# 顽固技术问题与架构演进反思录 (Post-Mortem & Architecture Reflection)

在 `ImmersiveDisplay`（全屏沉浸式显示与窗口控制工具）的开发与现代化改造过程中，我们遭遇并解决了一系列极其顽固的底层技术挑战。这些挑战深入到 **Win32 窗口机制**、**多线程上下文** 以及 **.NET 10 / Native AOT 编译底层**。

本篇反思录旨在总结这些经典问题的成因、排查路径及系统性解决方案，为未来的架构设计与维护提供宝贵的参考。

---

## 目录
1. [问题一：`DisableRuntimeMarshalling` 下的结构体对齐崩溃（PAINTSTRUCT）](#问题一disableruntimemarmarshalling-下的结构体对齐崩溃paintstruct)
2. [问题二：无 `SynchronizationContext` 下的 Win32 窗口线程亲和性崩溃](#问题二无-synchronizationcontext-下的-win32-窗口线程亲和性崩溃)
3. [问题三：多置顶（Topmost）窗口层级竞争与遮挡（Z-Order Race Condition）](#问题三多置顶topmost窗口层级竞争与遮挡z-order-race-condition)
4. [核心方法论与底层开发启示](#核心方法论与底层开发启示)

---

### 问题一：`DisableRuntimeMarshalling` 下的结构体对齐崩溃（PAINTSTRUCT）

#### 1. 现象描述
在引入 C# 12 Source-Generated COM（`[GeneratedComInterface]`）以支持 Native AOT 编译后，我们在全局程序集上启用了 `[assembly: DisableRuntimeMarshalling]`。
随后，一旦显示背景遮罩 Overlay 窗口，整个程序在触发重绘（`WM_PAINT` / `BeginPaint`）时会**发生瞬时、无任何异常信息的崩溃（Access Violation 内存越界访问）**，但在标准 JIT 调试模式下运行却完全正常。

#### 2. 原因剖析
在传统的 .NET 中，CLR 的内置封送拆收器（Runtime Marshaller）会在结构体传递给 P/Invoke 时自动做数据转换。例如，它会将 C# 的 1 字节 `bool` 字段封送为 Win32 的 4 字节 `BOOL` (`int`)。

然而，一旦声明了 `DisableRuntimeMarshalling`，为了 Native AOT 性能与安全，所有的**隐式重构与类型补齐都会被强行关闭**：
* C# 中声明为 `bool` 的字段，在底层直接以原生的 **1 字节**（Byte）写入内存。
* Win32 系统的原生 `PAINTSTRUCT` 结构如下：
  ```cpp
  typedef struct tagPAINTSTRUCT {
      HDC  hdc;
      BOOL fErase;      // 4字节 BOOL
      RECT rcPaint;     // 16字节 RECT
      BOOL fRestore;    // 4字节 BOOL
      BOOL fIncUpdate;  // 4字节 BOOL
      BYTE rgbReserved[32];
  } PAINTSTRUCT; // 理论大小：64-bit 下为 72 字节
  ```
* 当我们在 C# 中使用 `bool fErase` 时，因为封送处理被禁用，C# 映射的 `PAINTSTRUCT` 变成了 **63 字节**（丢失了由于 `bool` 导致的 9 字节对齐）。
* 当我们将 63 字节的结构体指针传给 native API `BeginPaint(hWnd, out var ps)` 时，操作系统写入了 72 字节，直接**写穿了托管栈空间（Stack Overflow / Memory Corruption）**，导致返回时破坏了返回地址，从而触发操作系统的硬件级保护崩溃。

#### 3. 解决方案与反思
* **对策**：将 [NativeMethods.WindowCreation.cs](file:///c:/Users/luokeke/RiderProjects/DisplayTrackTool/ImmersiveDisplay/Interop/NativeMethods.WindowCreation.cs) 中的 `bool` 字段全部强行重构为 **`int`**。这保证了结构体在原生状态下拥有精确的 72 字节对齐与排布。
* **反思**：Native AOT 下的底层互操作（Interop）容错率极低。编写结构体映射时，**不能依赖 C# 的高级语法糖与隐式转换，必须以原生 C 语言的字节对齐标准（Alignment & Size）来精确重绘所有结构体**。

---

### 问题二：无 `SynchronizationContext` 下的 Win32 窗口线程亲和性崩溃

#### 1. 现象描述
在引入异步探测机制后，用户频繁遇到开启背景窗口导致 WebView2 无响应，或者整个背景窗口变成一个黑色/白色死矩形，最后程序崩溃并弹出 Windows 错误弹窗。

#### 2. 原因剖析
Win32 窗口有一个极其严格的底层契约：**“窗口是由创建它的线程拥有的，并且其窗口消息（Window Message）只能由创建线程的消息泵（Message Pump）进行派发和处理”**。

在 `TargetStateManager` 中，所有的核心逻辑都是以 `async/await` 异步组织的（例如 `await Task.Run(...)` 用于轮询查找目标进程窗口）。
* 传统 WPF / WinForms 应用在启动时会自动注入一个 `SynchronizationContext`，确保 `await` 之后的延续任务（Continuation）会自动封送回 UI 线程运行。
* 但本项目采用了原生的 **裸 Win32 消息泵** 启动，默认**不具有任何同步上下文**。
* 因此，在 `StartAsync` 中的任意一个 `await` 之后，剩下的代码都会被自动分发到 **.NET 线程池线程（ThreadPool Thread）** 运行。
* 结果是，`overlayService.Show()` 最终在线程池线程上执行了 `CreateWindowEx`。
* 线程池线程很快被回收或挂起，并且该线程池线程上**根本没有运行 GetMessage/DispatchMessage 消息循环**。
* 由于没有消息泵派发 `WM_PAINT`、`WM_ERASEBKGND` 等消息，该窗口立刻发生无响应（变白），且当操作系统试图与该窗口通信时，会直接抛出非法线程操作或超时错误导致整个主程序崩溃。

#### 3. 解决方案与反思
* **对策**：在全局引入一个轻量级的 `UiDispatcher`，通过主窗口的回调（`WM_DISPATCH`）自制一个 UI 线程分发队列。随后将 `OverlayService.Show()` 和 `Hide()` 中的所有原生窗口操作（`CreateWindowEx`、`ShowWindow`、`DestroyWindow`）**强制封送回拥有消息泵的主 UI 线程**执行。
* **反思**：异步 `async/await` 与 Win32 窗口的“线程亲和性”（Thread Affinity）是天然对立的。在没有主流 UI 框架的环境下，**必须手工构建和维护同步通道，确保任何涉及 `HWND` 创建与销毁的操作绝不偏离主消息泵线程**。

---

### 问题三：多置顶（Topmost）窗口层级竞争与遮挡（Z-Order Race Condition）

#### 1. 现象描述
虽然主窗口和背景遮罩都可以正常显示，但是在某些情况下，背景遮罩 Overlay 会“篡位”盖在目标应用（如游戏或全屏目标程序）的上方，阻挡了用户的正常操作。

#### 2. 原因剖析
在 Win32 系统中，带有 `WS_EX_TOPMOST` 样式的置顶窗口属于同一个置顶层（Topmost Band）。当屏幕上有多个置顶窗口时，它们的层级次序依然受 Z 轴排序（Z-Order）约束，且非常敏感：
1. **异步创建错位**：为了修复上面的线程亲和性崩溃，Overlay 窗口是在 UI 线程中通过 `BeginInvoke` 异步生成的。当主线程优先对目标窗口设置了置顶并拉伸后，UI 线程随后才执行 `ShowWindow`。新出现的 Overlay 窗口会被 Windows 缺省推入置顶层的最顶端，从而遮盖了已经就绪的目标窗口。
2. **独立层级排列（无绑定）**：在原本的代码中，Z 轴重新排序是分离的。例如：
   ```csharp
   // 将 Overlay 移到 TOP (置顶层的顶端)
   SetWindowPos(overlay, 0, ...);
   // 将目标窗口移到 TOPMOST
   SetWindowPos(target, -1, ...);
   ```
   两个不相干的句柄各自定位，没有任何连带链条，当发生窗口切换、最小化恢复、或者高 DPI 分辨率同步延迟时，两者的相对先后顺序极易被操作系统打乱，导致遮挡。

#### 3. 解决方案与反思
* **对策**：实现 **“置顶状态同步与链式绑定”**：
  * 在 Overlay 窗口创建完毕或布局重构（`ApplyLayout` / `ApplyAggressiveLayout`）时，**总是先对目标窗口（Target）进行 Z 轴置顶排序**。
  * 紧随其后，读取目标窗口的置顶样式，让 Overlay 动态同步自己的置顶态。
  * 最后，以**链式关联**的形式，显式调用 `SetWindowPos`：
    ```csharp
    // 将 Overlay 窗口直接安插在 targetHwnd 的正后方 (hWndInsertAfter = targetHwnd)
    SetWindowPos(overlayHwnd, targetHwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    ```
  * 这建立了一个绝对的层级相对链：不管目标窗口是在置顶层还是普通层，Overlay 窗口都会如影随形地被 Windows 牢牢“扣”在它的后方，彻底斩断了相对位置发生颠倒的可能。
* **反思**：在处理窗口遮罩与多窗口布局时，**绝不要寄希望于“独立置顶”去碰运气，必须通过显式的 `hWndInsertAfter = TargetHwnd` 建立强关联的链式层级结构**。

---

## 核心方法论与底层开发启示

通过对这些顽固崩溃与错位 Bug 的狙击，我们为项目的底层架构沉淀了极其高标准的编写准则：

1. **Native AOT 的零容忍规则**：只要开启了 `DisableRuntimeMarshalling`，所有与外部 C/C++ API 交互的结构体字节大小（Byte Size）必须精确到个位数。推荐在单元测试或初始化中增加 `Marshal.SizeOf<T>()` 的断言校验。
2. **线程隔离原则**：异步方法（Async/Await）负责业务调度与网络/文件操作，而所有的 HWND 布局与 Win32 原生操作必须以类似“管道模式”的队列收口到 UI 线程统一串行执行。
3. **Z 轴防脱落链条**：在多窗口协同工作（如“遮罩背景-主工作窗口”）的场景下，所有的 Z 轴更变必须成对出现，并且以**次序窗口（Owner/Chain）**的模式锁定相对层级，拒绝各自为战。

这些宝贵的底层排查经验已被整合进 `DOC/` 目录中，在未来的功能开发（如多屏幕适配、异形屏幕裁切等）中，本篇反思录将作为核心 interop 设计规范持续发挥作用。
