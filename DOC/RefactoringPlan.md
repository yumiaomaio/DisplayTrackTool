# Refactoring Plan: C# AOT exe + C++ DLL, Two Threads

## Context

Current architecture: C++ EXE hosts WebView2 + message pump, C# is a DLL consumed via P/Invoke. All Win32 operations are marshaled to the C++ UI thread via `UiDispatcher` + hidden message window.

Goal: Invert the relationship — C# becomes the EXE (entry point), C++ becomes a DLL (WebView2 host). Thread isolation: business logic (C# Main), HWND operations (C# Overlay/Hook thread), WebView2 (C++ DLL thread). Remove the UiDispatcher workaround entirely.

This plan is based on the existing `DOC/RefactoringPlan.md` with refinements from codebase analysis.

## Analysis of Proposed Architecture

### Target Architecture (validated)
```
┌─────────────────────────────────────────────────┐
│ C# AOT EXE (ImmersiveDisplay.exe)               │
│                                                  │
│  Thread 1 (Main): 业务逻辑                       │
│  ├─ DI 容器 + Service 注册                       │
│  ├─ AppBridge 消息派发                           │
│  ├─ 所有 Service 调用 (Config, Display, Protocol)│
│  ├─ Event 处理                                   │
│  └─ 不碰任何 HWND                               │
│                                                  │
│  Thread 2 (Hook/Overlay): 独立消息泵             │
│  ├─ 键盘 Hook (WH_KEYBOARD_LL)                  │
│  ├─ WinEvent Hook (窗口监控)                    │
│  ├─ 覆盖窗口 (OverlayWindowShell)               │
│  ├─ 自己的 GetMessage 循环                      │
│  └─ 所有 HWND 操作在此线程完成                  │
│                                                  │
│  P/Invoke ──────────────────┐                   │
└──────────────────────────────┼──────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │ C++ DLL (host.dll)  │
                    │ Thread 3: WebView2  │
                    │ 消息泵               │
                    │  ├─ 主窗口 HWND      │
                    │  ├─ WebView2 COM     │
                    │  └─ GetMessage 循环  │
                    └─────────────────────┘
```

### Communication Paths (validated)
```
JS → C#:   JS postMessage → WebView2 COM 回调
           → C++ WndProc → C# function pointer callback

C# → JS:   C# → P/Invoke → C++ Host_PostMessage(json)
           → ExecuteScript

Main → Overlay:  PostMessage(overlayHwnd, WM_OVERLAY_CMD, ...)

Overlay → Main: 直接调方法 (无 HWND 操作不需要线程约束)
```

## Key Findings from Codebase Analysis

### 1. All UiDispatcher Usages Categorized

**Group A — Dialog operations (need HWND parent):**
| File | Method | Dialog type |
|------|--------|-------------|
| `AppIntegrationService.cs:154` | `SelectAssociatedProgram()` | ShowOpenFileDialog |
| `OverlayImageService.cs:36` | `SelectAndSetBackgroundImage()` | ShowOpenFileDialog |
| `ProcessService.cs:134` | `GetProcessCommandLine()` | ShowWarning |
| `AppBridge.cs:312` | `ShowAbout()` | ShowInfo |
| `AppBridge.cs:231` | `StartMonitoring()` | ShowError |
| `TargetStateManager.cs:208` | `StartAsync()` | ShowWarning |

All use `NativeDialogService` which calls `GetActiveWindow()` (returns NULL on threads without windows).

**Decision:** `MessageBoxW` and `GetOpenFileNameW` work fine with NULL parent HWND — dialog appears but is not modal. Accept this. No need for overlay-thread dispatch.

**Group B — HWND overlay operations (must move to overlay thread):**
| File | Method | Operation |
|------|--------|-----------|
| `OverlayService.cs:75` | `Show()` | Create/manage overlay window |
| `OverlayService.cs:131` | `Hide()` | Dispose overlay window |
| `WindowMonitorService.cs:33` | `StartMonitoring()` | SetWinEventHook (needs message pump) |
| `WindowMonitorService.cs:71` | `StopMonitoring()` | UnhookWinEvent |
| `WindowMonitorService.cs:120` | `DebounceTimer_Tick()` | Window state check |

These become custom window messages posted to the overlay thread's HWND.

**Group C — Async task dispatch (just remove UiDispatcher):**
| File | Method |
|------|--------|
| `AppBridge.cs:231` | `StartMonitoring()` |
| `AppBridge.cs:247` | `StopMonitoring()` |
| `AppIntegrationService.cs:38,56,101` | F9/F12/startup hooks |

These call `stateManager.StartAsync/StopAsync` which internally uses `Task.Run`/`Task.Delay` — no SynchronizationContext dependency. Wrapping in `UiDispatcher.BeginInvoke` was unnecessary.

```csharp
// Before
public void StartMonitoring(string processName)
{
    configService.SetDefaultProcessName(processName);
    UiDispatcher.BeginInvoke(async () => 
    {
        await stateManager.StartAsync(processName);
    });
}

// After
public void StartMonitoring(string processName)
{
    configService.SetDefaultProcessName(processName);
    _ = Task.Run(async () => await stateManager.StartAsync(processName));
    // Or just: _ = stateManager.StartAsync(processName);
}
```

**Group D — Logging (use lock):**
`LoggingService.cs:48` — marshals `ObservableCollection` access via UiDispatcher. Replace with `Lock` + direct collection access. `CollectionChanged` event fires on the caller's thread, which is fine since `AppBridge.OnLogsChanged` just serializes state.

### 2. NativeDialogService — HWND Parent Issue

Current: `GetActiveWindow()` gets the calling thread's active window.
After refactoring: Main thread has no windows → `GetActiveWindow()` returns NULL.

`MessageBoxW(NULL, ...)` — works, just not modal.
`GetOpenFileNameW` with `hwndOwner = NULL` — works, just not modal.

**Verdict:** Acceptable for this tool. No changes needed to `NativeDialogService`.

### 3. OverlayWindowShell — Thread Safety

`OverlayWindowShell` uses `[ThreadStatic] _creatingInstance` (correct for thread-local instance during CreateWindowEx). The `_classRegistered` field is static (shared across threads) but `RegisterClassEx` is process-wide anyway — second registration will fail, which is handled.

**Action:** No changes to OverlayWindowShell logic. Just invoke Create/Show/Dispose on the overlay thread.

### 4. KeyboardHookService — Thread Affinity

`WH_KEYBOARD_LL` hook callback is called on the thread that installed it via its message queue. The hook must be installed and uninstalled on the same thread.

**Action:** Move `KeyboardHookService` lifecycle to overlay thread.

### 5. Existing C++ Code to Preserve

The following C++ code becomes `host.dll` (merged):

| Existing file | Fate |
|---------------|------|
| `src/main.cpp` | Becomes `Host_Start()` — remove `wWinMain`, keep init order |
| `src/AppWindow.cpp` | Keep as-is — window creation + message pump |
| `src/WebViewHost.cpp` | Keep as-is — WebView2 init + message routing |
| `src/InteropHelper.cpp` | Keep as-is |
| `include/ImmersiveEngine.h` | Delete — no longer imports C# DLL |
| `resource.rc` | Move icon to C# project |
| `CMakeLists.txt` | Delete — build is now `dotnet publish` with cl.exe step |

### 6. Existing C# Files to Modify/Remove

| File | Action |
|------|--------|
| `NativeExports.cs` | **Delete** — no longer a DLL with exports |
| `Helpers/UiDispatcher.cs` | **Delete** |
| `Helpers/HiddenMessageWindow.cs` | **Delete** |
| `Helpers/UiSynchronizationContext.cs` | **Delete** |
| `ImmersiveEngine.cs` | **Rewrite** — becomes the exe entry point orchestrator |
| `.csproj` | Change to `<OutputType>Exe</OutputType>` + add C++ build step + remove `ImmersiveDisplay.lib` linking |

### 7. Files Not Modified by Refactoring

These files have zero threading concerns and need no changes:
- `Services/Implementations/ConfigService.cs` — pure data
- `Services/Implementations/DisplayService.cs` — display topology
- `Services/Implementations/LaunchService.cs` — process launch
- `Services/Implementations/ProtocolService.cs` — registry ops
- `Services/Implementations/TaskbarService.cs` — taskbar control
- `Services/Implementations/WindowLayoutManager.cs` — window positioning via P/Invoke (already thread-safe)
- `Services/Implementations/WindowQueryService.cs` — window search
- `Services/Interfaces/*` — interfaces unchanged
- `Models/*` — data models
- `Interop/*` — P/Invoke declarations
- `Bridge/AppBridge.cs` — structural changes only (remove UiDispatcher calls)
- `Views/OverlayWindowShell.cs` — structural changes only (invoked from overlay thread)

## Implementation Plan

### Step 1: Create `Program.cs` — C# AOT exe entry

```csharp
[STAThread]
static void Main()
{
    // 1. Start Overlay/Hook thread (independent message pump)
    var overlayHost = new OverlayHost();
    overlayHost.Start();

    // 2. Initialize business logic
    var engine = new ImmersiveEngine(overlayHost);
    engine.Initialize();

    // 3. Start C++ window (blocks — internal message pump loop)
    NativeHost.Start(
        &OnJsMessage,    // JS → C# callback
        &OnWindowResized // window resize notification
    );
    // Returns when main window closes

    engine.Dispose();
    overlayHost.Stop();
}

[UnmanagedCallersOnly]
static void OnJsMessage(IntPtr jsonPtr)
{
    string json = Marshal.PtrToStringUTF8(jsonPtr);
    string result = _engine.Bridge.HandleMessage(json);
    // Response forwarded via callback
}

[UnmanagedCallersOnly]
static void OnWindowResized(int w, int h)
{
    // Notify overlay to reposition if needed
}
```

### Step 2: Create `OverlayHost.cs` — Hook/Overlay thread

Key design decisions:
- Custom window messages: `WM_OVERLAY_SHOW`, `WM_OVERLAY_HIDE`, `WM_OVERLAY_UPDATE_POSITION`
- Receives `TargetStateManager` reference to fire events directly (no thread constraint)
- `KeyboardHookService.Start/Stop` called on overlay thread
- `WindowMonitorService.StartMonitoring/StopMonitoring` called on overlay thread
- Thread exits when `PostMessage(WM_QUIT)` is received

```csharp
public class OverlayHost : IDisposable
{
    private Thread _thread;
    private IntPtr _overlayHwnd = IntPtr.Zero;
    private TaskCompletionSource<IntPtr> _hwndReady = new();

    public IntPtr OverlayHwnd => _overlayHwnd;
    public IntPtr WaitForHwnd() => _hwndReady.Task.Result;

    public void Start()
    {
        _thread = new Thread(ThreadProc);
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Name = "OverlayHost";
        _thread.Start();
    }

    private void ThreadProc()
    {
        // Register a message-only window class for dispatching overlay commands
        // Create the HWND
        // Signal readiness: _hwndReady.TrySetResult(_overlayHwnd)
        
        while (GetMessage(out MSG msg, ...))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    public void Stop()
    {
        PostMessage(_overlayHwnd, WM_QUIT, ...);
        _thread.Join(2000);
    }
}
```

### Step 3: Create `NativeHost.cs` — C++ DLL P/Invoke

```csharp
internal static class NativeHost
{
    [DllImport("host.dll")]
    public static extern IntPtr Host_Start(
        delegate* unmanaged<IntPtr, void> onMessage,
        delegate* unmanaged<int, int, void> onResized);

    [DllImport("host.dll")]
    public static extern void Host_PostMessage(IntPtr ctx, IntPtr jsonPtr);

    [DllImport("host.dll")]
    public static extern void Host_Shutdown(IntPtr ctx);
}
```

### Step 4: Create C++ `host.dll`

Merge `main.cpp` + `AppWindow.cpp` + `WebViewHost.cpp` into a DLL:
- Remove `wWinMain` — entry point becomes `Host_Start`
- `OnImmersiveMessage` becomes a function pointer callback
- `Host_Start` blocks with its own message pump
- Export: `Host_Start`, `Host_PostMessage`, `Host_Shutdown`

### Step 5: Modify services — Remove UiDispatcher

**`LoggingService.cs`**: Replace `UiDispatcher.BeginInvoke()` with `lock (_lock) { Logs.Insert(0, ...) }`.

**`OverlayService.cs`**: Replace `UiDispatcher.BeginInvoke()` with `PostMessage(_overlayHost.OverlayHwnd, WM_OVERLAY_SHOW/HIDE, ...)`.

**`WindowMonitorService.cs`**: 
- Move `StartMonitoring/StopMonitoring` to be called on overlay thread
- Or make it a thin facade that posts messages
- WinEvent hooks need message pump → threads are compatible

**`AppIntegrationService.cs`**: F9/F12 hooks remove `UiDispatcher.BeginInvoke`. Call `stateManager.StartAsync/StopAsync` directly.

**`OverlayImageService.cs`**: `SelectAndSetBackgroundImage` — `ShowOpenFileDialog` works with NULL parent, remove `UiDispatcher.BeginInvoke`.

**`AppBridge.cs`**: Remove `UiDispatcher.BeginInvoke` from `StartMonitoring`, `StopMonitoring`, `ShowAbout`.

### Step 6: Modify `ImmersiveEngine.cs`

- Remove `UiDispatcher.Initialize()`
- Inject `OverlayHost` into services that need it
- Remove `UiDispatcher.Shutdown()`
- Change Initialize order: create DI → create bridge → init hooks

### Step 7: Modify csproj

```xml
<OutputType>Exe</OutputType>
<!-- Add C++ build step -->
<Target Name="BuildNativeHost" BeforeTargets="Build">
    <Exec Command="cl.exe /LD /EHsc /Fe:host.dll
        ../immersive-cpp-wrapper/src/*.cpp
        /I ../immersive-cpp-wrapper/include
        /I packages/WebView2/build/native/include
        User32.lib Shell32.lib Ole32.lib Shlwapi.lib
        WebView2Loader.dll.lib" />
</Target>
```

### Step 8: Delete obsolete files

- `NativeExports.cs`
- `UiDispatcher.cs`
- `HiddenMessageWindow.cs`
- `UiSynchronizationContext.cs`
- `include/ImmersiveEngine.h`
- `CMakeLists.txt`

## Implementation Order

| Step | What | Risk |
|------|------|------|
| 1 | Create `OverlayHost.cs` (new file) | Low — new code, doesn't affect existing |
| 2 | Create `NativeHost.cs` (new file) | Low — just P/Invoke declarations |
| 3 | Create C++ `host.dll` (from existing C++ sources) | Medium — DLL exports vs EXE entry |
| 4 | Modify `LoggingService.cs` — remove UiDispatcher | Low — lock-based |
| 5 | Modify `OverlayService.cs` — PostMessage to overlay thread | Medium — message protocol design |
| 6 | Modify `WindowMonitorService.cs` — move to overlay thread | Medium — threading migration |
| 7 | Modify `AppBridge.cs` + `AppIntegrationService.cs` — remove UiDispatcher | Low |
| 8 | Modify `OverlayImageService.cs` + `ProcessService.cs` — remove UiDispatcher | Low |
| 9 | Modify `ImmersiveEngine.cs` — remove UiDispatcher init/shutdown | Low |
| 10 | Create `Program.cs` — exe entry | Medium — orchestrates startup sequence |
| 11 | Modify csproj — Exe output + C++ build step | Low |
| 12 | Delete obsolete files | Low |

## Risks & Mitigations

1. **Cross-thread overlay Z-order**: `SetWindowPos(overlayHwnd, targetHwnd, ...)` works cross-thread in Windows. The overlay thread can set Z-order relative to a target HWND from another thread. Verified behavior — no issue.

2. **Dialog parent HWND is NULL**: MessageBox and GetOpenFileName work fine with NULL parent. The dialog is not modal to any window, but for this tool that's acceptable. Users interact primarily through the WebView2 UI.

3. **Keyboard hook uninstall thread affinity**: `UnhookWindowsHookEx` must be called from the same thread as `SetWindowsHookEx`. Solved by keeping both on overlay thread.

4. **WinEvent hook uninstall thread affinity**: Similar to keyboard hook — keep on overlay thread.

5. **OverlayHost.Stop() race**: Need to ensure `PostMessage(WM_QUIT)` is called after the overlay HWND is created. Use `_hwndReady` TaskCompletionSource to synchronize.

6. **Build toolchain dependency**: `cl.exe` must be available on path. CMake currently handles MSVC detection. The csproj build step should use the same VS dev environment. Mitigation: Use `%VCToolsInstallDir%\bin\Hostx64\x64\cl.exe` or detect via `vswhere`.

## Verification

1. `dotnet publish -c Release -r win-x64` completes without errors
2. Host DLL is built and copied to output
3. WebUI assets are present in output/WebUI/
4. Application launches: WebView2 window appears, C# engine initializes
5. F9 starts monitoring, overlay window appears on target monitor
6. F12 stops monitoring, overlay disappears, state restores
7. Settings changes persist across restart
8. No `UiDispatcher` or `HiddenMessageWindow` references remain in codebase
9. Dialog operations (select image, select program) work without errors
