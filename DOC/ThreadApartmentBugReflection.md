# 技术反思：WebView2 STA 线程套间通信失效问题分析

## 1. 问题背景 (Background)
在开发 DisplayTrackTool 的过程中，前端（Vue 3）与 C# 核心引擎需要通过 C++ 原生宿主（`host.dll`）进行双向实时通信。
在近期调试中发现，前端的倒计时（`waitingCountdown`）以及实时监控日志（Runtime Logs）无法在界面上实时刷新。

* **C# 侧表现**：断点证实 `OnWaitingCountdownChanged` 和 `OnLogsChanged` 被正常触发，JSON 字符串构建完整（如 `{"waitingCountdown": 10}`），且成功执行了 `SendToHost` 飞递操作。
* **JS 侧表现**：无论是重写全局 `window.onStateChangedFromDll` 还是监控 `waitingCountdown` 响应式变量，前端控制台都**完全接收不到任何变化信号**。

---

## 2. 根本原因剖析 (Root Cause Analysis)

### 2.1 WebView2 / COM STA 线程限制
WebView2 基于 Windows COM（组件对象模型）的 **单线程套间（STA, Single-Threaded Apartment）** 架构。
根据 STA 的安全与线程同步要求：
* **核心限制**：所有对 WebView2 实例的操作（包括调用 `ExecuteScript` 注入脚本或执行导航）**必须且只能**在创建该 WebView2 实例的同一个线程（即主 UI 线程，跑 Win32 消息循环的线程）上执行。
* **越界后果**：如果从任意后台线程直接调用 WebView2 的 COM 接口，底层将直接拒绝执行，并返回错误码 `RPC_E_WRONG_THREAD` (`0x8001010E`)。

### 2.2 多线程设计冲突
本项目的底层架构是一个典型的混合并发系统：
1. **主 UI 线程**：在 C++ 的 `AppWindow::Run()` 消息泵中运行，负责 WebView2 的生命周期和渲染。
2. **后台线程池 (ThreadPool) 线程**：C# 引擎在执行 `StartAsync` 时，为了不阻塞 UI，使用了 `Task.Run` 将检测循环、日志记录等监控工作交给了后台线程执行。

当 C# 后台线程产生新状态时，它在后台线程直接触发了 P/Invoke 接口 `Host_PostMessage`，导致 C++ 尝试在**非 UI 线程**中直接调用 `m_webView->ExecuteScript`：

```mermaid
sequenceDiagram
    participant C# ThreadPool as C# (ThreadPool)
    participant C++ Host as C++ (Non-UI Thread)
    participant WebView2 as WebView2 (STA / UI Thread)
    
    C# ThreadPool->>C++ Host: P/Invoke Host_PostMessage(json)
    C++ Host->>WebView2: m_webView->ExecuteScript(script) [WRONG THREAD]
    Note over WebView2: Silent Fail!<br/>Returns RPC_E_WRONG_THREAD (0x8001010E)
```

### 2.3 致命的“静默失败” (Silent Failure)
之所以该问题在静态代码走查和初级测试中极难被发现，是因为系统存在多层隐式吞没：
1. `WebViewHost::ExecuteScript` 返回了 `HRESULT` 错误状态，但在 `Host_PostMessage` 导出函数中被**直接丢弃**，没有进行任何日志记录。
2. C# 侧将 `Host_PostMessage` 声明为了 `void`，导致非托管代码返回的任何错误都无法被 C# 异常机制捕获。
3. 整个跨线程破坏过程**不报错、不崩溃、不抛出异常**，形成了完美的调试盲区。

---

## 3. 解决方案 (The Marshalling Solution)

为了将任意线程的调用安全地同步/邮寄（Marshalling）到主 UI 线程，我们在 C++ 侧建立了基于 **Win32 窗口消息队列** 的异步管道：

### 3.1 窗口自定义消息
在 `host.cpp` 中定义一个用户级窗口消息，用于标识执行脚本任务：
```cpp
#define WM_EXECUTE_SCRIPT (WM_USER + 1)
```

### 3.2 异步消息封送 (Marshalling)
重写 `Host_PostMessage` 导出函数。当其被后台线程调用时，不再直接操作 WebView2，而是将脚本内容分配至堆内存，并利用 **`PostMessageW`** 线程安全地投递给主窗口的消息队列：

```cpp
__declspec(dllexport) void __stdcall Host_PostMessage(void* ctx, const char* jsonUtf8)
{
    if (!ctx || !jsonUtf8) return;
    auto* host = static_cast<HostContext*>(ctx);
    std::wstring jsonWide = InteropHelper::Utf8ToWide(jsonUtf8);
    if (!jsonWide.empty()) {
        std::wstring script = std::format(
            L"if(window.onStateChangedFromDll) {{ window.onStateChangedFromDll({}); }}",
            jsonWide);
        
        // 1. 将脚本字符串打包分配至堆内存
        auto* scriptPtr = new std::wstring(script);
        
        // 2. 邮寄到 UI 线程的消息队列。如果投递失败，立即释放内存防止泄漏
        if (!PostMessageW(host->appWindow.GetHwnd(), WM_EXECUTE_SCRIPT, 0, reinterpret_cast<LPARAM>(scriptPtr))) {
            delete scriptPtr;
        }
    }
}
```

### 3.3 UI 线程接管与执行
主窗口在接收到该消息后，会自动在其所属的 **主 UI 线程** 中触发消息回调，安全地执行脚本并释放内存：

```cpp
ctx->appWindow.SetCustomMessageCallback([ctx](UINT msg, WPARAM wp, LPARAM lp) {
    if (msg == WM_EXECUTE_SCRIPT) {
        auto* scriptPtr = reinterpret_cast<std::wstring*>(lp);
        if (scriptPtr) {
            // 安全地在主 UI 线程（创建 WebView2 的线程）上调用
            ctx->webViewHost.ExecuteScript(*scriptPtr);
            // 释放堆内存
            delete scriptPtr;
        }
    }
});
```

---

## 4. 技术反思与教训 (Key Takeaways)

1. **防范非托管交互中的“静默吞没”**：
   在编写 P/Invoke 和 C++/C# 混合代码时，绝不能忽略 `HRESULT` 或底层返回状态。如果能够建立严密的 Native 日志机制（即使是简单的 `std::println` 输出非零 HRESULT），该问题在开发首日就能暴露。
   
2. **并发架构设计中的线程所有权约束**：
   在使用嵌入式浏览器框架（如 WebView2、CEF 等）或原生 UI 框架（WPF、WinForms）时，必须在设计之初为所有的“推模式”（Push Model）状态更新明确**线程归属（Thread Ownership）**。任何从后台计算线程流向界面的数据，都必须具备显式的 Thread Marshalling 机制。

3. **对运行时事实保持敏感**：
   在静态分析遇到瓶颈时，不要陷入静态语义的局部纠缠。应高度珍视并利用“C# 端百分百有，JS 端百分百无”这类运行边界状态，迅速锁定通信沙箱与底层传输机制，这往往是推倒技术难题的临门一脚。
