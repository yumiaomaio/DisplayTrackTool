# Win32 线程与消息泵知识整理

## 1. 什么是 UI 线程

UI 线程不是"负责画界面的线程"，而是**唯一跑消息泵的线程**：

```cpp
while (GetMessage(&msg, nullptr, 0, 0))  // 从线程消息队列取消息，没有就睡
{
    TranslateMessage(&msg);              // 键盘按键 → 字符消息
    DispatchMessage(&msg);               // 找到 HWND 的 WndProc 并调用
}
```

你的线程跑了这段循环，就是 UI 线程；没跑，就不是。没有模棱两可的定义。

**GetMessage 的行为**：
- 队列有消息 → 取出返回
- 队列为空 → 线程挂起等待，直到有新消息出现

**为什么需要消息泵**：Windows 的输入（键盘鼠标）、窗口事件（`WM_SIZE`、`WM_PAINT`、`WM_DESTROY`）、COM 回调，都通过消息队列串行化到消息泵循环里处理。没有消息泵，这些消息永远排着队没人处理。

## 2. HWND 线程亲缘性

HWND 是**内核对象句柄**，属于创建它的线程：

```cpp
// 线程 A:
HWND hwnd = CreateWindowEx(...);  // hwnd 绑定到线程 A
while (GetMessage(&msg, ...)) { DispatchMessage(&msg); }

// 线程 B:
SetWindowPos(hwnd, ...);  // 非法！hwnd 属于线程 A
```

这是 Win32 USER32 模块的硬规则，跟 COM 无关。原因：USER32 设计于 1985 年（单核单线程时代），所有 HWND 操作假设在同一个线程上，不需要锁。

**跨进程操作窗口为什么可以**：`SetWindowPos(hwndOfOtherProcess, ...)` 内部通过内核对象把消息 post 到目标进程的消息队列，由目标进程自己的 UI 线程实际执行。你不是直接操作别人的 HWND，而是请求对方操作。

| 场景 | 实际执行线程 | 允许？ |
|---|---|---|
| 移动本进程的窗口 | 必须是创建 HWND 的线程 | 强制 |
| 移动其他进程的窗口 | 对方 UI 线程（经消息队列） | 可以 |
| 移动其他进程的窗口（本线程直接调 API） | 实际在对方线程执行 | 安全 |

## 3. 消息泵的三步

### GetMessage
从线程消息队列取消息。队列空则线程挂起。

### TranslateMessage
将键盘按键消息（`WM_KEYDOWN`）翻译为字符消息（`WM_CHAR`）。不涉及 HWND 操作。非键盘消息忽略。

### DispatchMessage
根据 `MSG.hwnd` 找到对应窗口类注册时的 `WndProc` 函数指针，**直接函数调用**：

```cpp
// DispatchMessage 内部（极度简化）
WndProc wndProc = FindWndProc(msg->hwnd);
return wndProc(msg->hwnd, msg->message, msg->wParam, msg->lParam);
```

注意：这是**同步函数调用**，不是投递消息。`DispatchMessage` 在 WndProc 返回后才返回。所以 WndProc 里做耗时操作会卡住整个消息泵。

## 4. COM STA 与隐藏窗口

### STA 的定义
- COM 的线程模型之一。调 `CoInitializeEx(NULL, COINIT_APARTMENTTHREADED)` 标记当前线程为 STA
- STA 线程**必须**跑消息泵
- STA 线程上的 COM 对象只能在该线程上被调用

### COM 如何跨 STA 封送调用

如果你从后台线程调 STA 线程上的 COM 对象的方法：

```cpp
// 后台线程
m_webView->Navigate(url);
```

1. COM 检测到调用线程和目标 STA 线程不同
2. COM 把 `Navigate` 的参数打包到内部结构
3. COM 通过 `PostMessage` 投递一条内部消息给它的**隐藏窗口**（`"OleMainThreadWndClass"`）
4. 你的消息泵 `GetMessage` 取到 → `DispatchMessage` → COM 隐藏窗口的 WndProc
5. COM 解包参数，在正确的 STA 线程上执行 `Navigate`
6. 结果通过消息封送回调用线程

**关键**：COM 的隐藏窗口也是在当前线程上创建的（`CoInitializeEx` 时内部调 `CreateWindowEx`）。所以它和其他 HWND 一样，属于你的消息泵线程。`PostMessage` 投到的是同一个线程消息队列。

### 为什么 COM 不能用你的窗口
COM 需要一个自己完全控制的 WndProc，来处理它自己的内部消息。如果 COM 把消息发给你的主窗口，你的 WndProc 不认识 `WM_COM_CALLBACK`，会 `DefWindowProc` 忽略掉。COM 注册自己的窗口类 + WndProc 是模块化的正常做法。

### 这是 Win32 还是 COM 的机制
**两者配合的结果**：
- Win32 提供：消息队列基础设施（`GetMessage`/`PostMessage`/`DispatchMessage`）
- COM 利用这个：创建隐藏窗口 + PostMessage → 消息泵 → DispatchMessage 到隐藏窗口 WndProc

如果你调了 `CoInitializeEx` 但不跑消息泵，COM 的跨线程调用永远排着队没人处理。反过来，你跑消息泵但没调 `CoInitializeEx`，WebView2 创建 `CreateCoreWebView2Controller` 会失败。

## 5. PostMessage vs SendMessage vs PostThreadMessage

### PostMessage
```cpp
PostMessage(hwnd, WM_XXX, wParam, lParam);
```
- 投递消息到 `hwnd` 所属线程的消息队列末尾
- **立即返回**，不等待处理
- 目标线程可以是任意线程（跨线程安全）
- `DispatchMessage` 根据 `MSG.hwnd` 找到 WndProc

### SendMessage
```cpp
SendMessage(hwnd, WM_XXX, wParam, lParam);
```
- 直接调目标 HWND 的 WndProc
- **阻塞等待** WndProc 返回
- 如果 hwnd 属于其他线程，会跨线程阻塞（当前线程等待，对方处理完后返回）
- 在当前线程上直接调 WndProc（对方 HWND 时通过消息封送）

### PostThreadMessage
```cpp
PostThreadMessage(threadId, WM_XXX, wParam, lParam);
```
- 投递到指定线程的消息队列，**不指定 HWND**
- `MSG.hwnd = NULL`
- `DispatchMessage` **无法处理**（不知道发给哪个窗口）
- 只能通过 `GetMessage` / `PeekMessage` 直接读取 `MSG` 结构体

## 6. 消息泵的饥饿问题（Trick）

### 纯 GetMessage 的问题
```cpp
while (GetMessage(&msg, nullptr, 0, 0))  // 没消息就睡
{
    DispatchMessage(&msg);
}
```
如果消息泵空闲时后台线程产生了一个需要 UI 线程处理的任务（比如 C# 的 `await` 后续），因为 `GetMessage` 阻塞着，任务不会被执行，直到有新消息唤醒它。

### 解决方案

**方案 A：PeekMessage + Sleep**
```cpp
while (true)
{
    onTick();  // 处理 C# 任务
    while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
    {
        if (msg.message == WM_QUIT) return;
        DispatchMessage(&msg);
    }
    Sleep(1);  // 避免 CPU 100%
}
```

**方案 B：MsgWaitForMultipleObjects**
```cpp
while (true)
{
    onTick();
    DWORD ret = MsgWaitForMultipleObjects(0, nullptr, FALSE, 5, QS_ALLINPUT);
    if (ret == WAIT_OBJECT_0)
    {
        while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE)) { ... }
    }
}
```

**方案 C：PostMessage 给自己**（当前项目的方式）
后台线程调 `PostMessage(hiddenHwnd, WM_DISPATCH, ...)` 投一条消息到队列，`GetMessage` 被唤醒返回，`DispatchMessage` 到目标 HWND 的 WndProc，在 WndProc 里执行 C# 任务。

### 注意
没有消息时 `GetMessage` 阻塞是正常行为——不是 bug。只有当你不希望 C# 任务被延迟时才需要考虑饥饿问题。大部分 Win32 应用（有用户交互）消息泵很少长时间空闲。

## 7. UiDispatcher + HiddenMessageWindow 机制分析

### 当前项目的方案

```
后台线程（await 后续/线程池）
  → UiDispatcher.BeginInvoke(action)
    → ConcurrentQueue 入队
    → PostMessage(hiddenHwnd, WM_DISPATCH, ...)
      → C++ 消息泵 GetMessage 取到
        → DispatchMessage → hiddenHwnd 的 WndProc
          → uiDispatcher.InvokePending()
            → 出队执行 action ← 此时在消息泵线程上
```

### 为什么要隐藏窗口

因为 C# DLL 需要**一个属于消息泵线程的 HWND** 来接收消息。它没有主窗口（主窗口是 C++ exe 创建的），所以自己注册了一个消息专用窗口（`HWND_MESSAGE`）。

`HWND_MESSAGE` 是不可见的、不在窗口枚举中的消息专用窗口。它的唯一作用是提供一个 HWND 收 `PostMessage`。

### 为什么要 UiSynchronizationContext

```csharp
class UiSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        UiDispatcher.BeginInvoke(() => d(state));
    }
}
```

C# 的 `await` 在恢复执行时会检查 `SynchronizationContext.Current`。如果有，就调 `context.Post(continuation)` 把后续代码送回那个上下文。`UiSynchronizationContext` 告诉 `await`："回到消息泵线程，别在线程池上跑。"

### 这个机制的局限性

- **额外的 HWND**：需要注册窗口类 + CreateWindowEx
- **两次 PostMessage 往返**（如果发生在后台）：日志线程 Post 给隐藏窗口，再 Dispatch 出来
- **只能 Post 到主消息泵线程**，不灵活
- **对新人理解成本高**：需要了解消息泵、PostMessage、WndProc、DispatchMessage、SynchronizationContext 的全链路

## 8. 为什么当前项目大部分 UiDispatcher.BeginInvoke 不是必要的

检索项目中所有的 `UiDispatcher.BeginInvoke` 调用：

| 位置 | 原因 | 重构后 |
|---|---|---|
| `OverlayService.Show` | 创建/移动覆盖窗口（HWND） | PostMessage 到 Overlay 线程 |
| `OverlayService.Hide` | 销毁覆盖窗口（HWND） | PostMessage 到 Overlay 线程 |
| `WindowMonitorService.Start/Stop` | SetWinEventHook 需要消息泵 | 在 Overlay 线程上直接调 |
| `LoggingService.AddLog` | ObservableCollection 不是线程安全 | 加锁或 ConcurrentQueue |
| `AppBridge.StartMonitoring` | 异步操作后续需要 UI 线程 | 不需要，主线程不碰 HWND |
| `AppIntegrationService.F9/F12` | 怕阻塞消息泵 | Task.Run 即可 |
| `OverlayImageService` | 文件操作 | 直接调 |
| `ProcessService` | 文件操作 | 直接调 |

分析后发现，**真正需要封送的只有覆盖窗口的操作**（HWND 亲缘性）。Log 服务加锁解决，其他服务根本不需要回到任何特定线程。

## 9. async/await 在多线程模型中的意义

当前单线程模型中 `await` 的角色：

- **不是并行**：所有代码仍在消息泵线程上串行
- **不阻塞消息泵**：`await Task.Delay(5000)` 让出线程，5 秒后回到同一线程继续执行
- 等价于 `SetTimer` + `WM_TIMER`——一个异步延时，而不是"把工作分到另一个 CPU"

重构后：

- `await` 后续可以在任意线程池线程上执行（没有 `SynchronizationContext` 约束）
- 主线程和 Overlay 线程都不需要知道 `await` 的存在
- 业务代码可以直接 `await File.ReadAllBytesAsync`、`HttpClient.GetAsync`，不需要关心封送

## 10. COM 跨线程回调（WebView2 回调）

WebView2 的 `WebMessageReceived` 等 COM 事件，内部触发时可能来自 WebView2 自己的工作线程。

COM 检测到事件处理函数属于 STA 线程，自动通过隐藏窗口 + PostMessage 封送到你的消息泵线程执行。

所以 JS → WebView2 → C++ → C# 这条路径天然在正确的线程上，不需要额外的 PostMessage。

当前项目中所有 WebView2 COM 操作（`Navigate`、`ExecuteScript`、`Resize`）也都是在消息泵线程上调的，COM 封送从未实际介入——只有 **COM 回调到 C++** 走了 COM 封送。

## 11. 拆分多消息泵的优势

### 当前：单消息泵
```
全部代码 → 一个消息泵线程 → HWND 操作安全，但所有线程约束传染到全局
```

### 重构后：按需分配消息泵
```
C++ DLL 消息泵：只管理主窗口 + WebView2
OverlayHost 消息泵：只管理键盘 hook + WinEvent + 覆盖窗口
C# 主线程：无消息泵，纯业务逻辑
```

这样每个消息泵只需要关注它自己的 HWND，不存在一个线程的 HWND 约束波及到无关代码的情况。理解粒度从"整体单线程"降级到"每个 HWND 只在一个线程上操作"。
