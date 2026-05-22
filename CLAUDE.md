# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

### Full Build (CMake orchestrates all three tiers)
```powershell
cd immersive-cpp-wrapper
mkdir -p build
cd build
cmake ..
cmake --build . --config Release
```
Output lands in `immersive-cpp-wrapper/build/Release/`.

### Individual Component Builds
- **Frontend only**: `cd vite-project && npm install && npm run build`
- **C# AOT only**: `cd ImmersiveDisplay && dotnet publish -c Release -r win-x64 --self-contained`
- **C++ only (skip frontend/C# rebuild)**: `cd immersive-cpp-wrapper/build && cmake .. -DBUILD_CPP_ONLY=ON && cmake --build . --config Release`

### Dev Server (frontend)
```powershell
cd vite-project
npm install
npm run dev
```

## Architecture (Three-Tier)

### 1. C++ Native Host (`immersive-cpp-wrapper/src/`)
Win32 app hosting WebView2. Entry point is `main.cpp`. Manages:
- `AppWindow` - Win32 window lifecycle
- `WebViewHost` - WebView2 control setup (async via C++20 coroutines), message routing, navigation interception
- `InteropHelper` - UTF8/Wide string conversion, path utilities
- Message flow: JS postMessage → C++ `WebViewHost` → `immersive_handle_message()` (C# export) → response forwarded back

### 2. C# Core Logic (`ImmersiveDisplay/`)
.NET 10 Native AOT library (`ImmersiveDisplay.dll`).
- **Entry**: `NativeExports.cs` - 6 `[UnmanagedCallersOnly]` exports: `Create`, `Initialize`, `HandleMessage`, `FreeString`, `Dispose`, `SetProtocolAutoStart`
- **Engine**: `ImmersiveEngine.cs` - DI setup, initializes all services and the `AppBridge`
- **Bridge**: `AppBridge.cs` - JSON command dispatcher (switch on `BridgeAction` enum), maps frontend actions to service calls, pushes state changes back via `OnMessageSent` event
- **Services** (`Services/`): Interfaces in `Services/`, implementations in `Services/Implementations/`. Key services: `ITargetStateManager` (orchestrates start/stop flow), `IConfigService` (persistent config), `IDisplayService` (display topology), `IOverlayService` (overlay window), `IWindowMonitorService` (target window tracking), `ITaskbarService` (taskbar auto-hide), `IKeyboardHookService`, `IProtocolService` (URI protocol registration)
- **Interop** (`Interop/`): P/Invoke structs and native method declarations. Struct alignment must match Win32 exactly (use `int` not `bool`, `[StructLayout(LayoutKind.Sequential)]`)
- **Helpers**: `UiDispatcher` (marshal work to UI thread via hidden window + `PostMessage`), `DpiHelper`, `ShortcutResolver`
- **Key constraint**: `[assembly: DisableRuntimeMarshalling]` means manual byte-level struct alignment for all P/Invoke

### 3. Vue Frontend (`vite-project/src/`)
- `App.vue` - Root component, manages global state, modal dialogs, RPC orchestration
- `components/` - `SetupView`, `RunningView`, `LogsView`, `OverlayModal`, `AppHeader`
- `services/bridge.js` - Dynamic `Proxy`-based RPC. Any `bridge.MethodName(payload)` becomes a JSON `{action, payload, callId}` message via `window.chrome.webview.postMessage`
- `i18n.js` - Internationalization
- Built with `vite-plugin-singlefile` → all assets bundled into one HTML file

## Communication Flow
```
Vue (bridge.js) --postMessage--> C++ (WebViewHost) --P/Invoke--> C# (NativeExports.HandleMessage)
                                                                          |
C# (AppBridge) --switch action--> Service method call
                                                                          |
Vue (onStateChangedFromDll) <--ExecuteScript-- C++ (OnImmersiveMessage) <-- C# (OnMessageSent event)
```

## Conventions

- **Threading**: All Win32 HWND operations must run on the main UI thread. Use `UiDispatcher.BeginInvoke()` from background threads.
- **C# Interop**: `[UnmanagedCallersOnly]` for exports. Use `Marshal.StringToCoTaskMemUTF8` / `Marshal.PtrToStringUTF8` for strings.
- **Struct alignment**: With `DisableRuntimeMarshalling`, use `int` instead of `bool`, verify `Marshal.SizeOf<T>()` matches Win32 layout.
- **Services**: Add as interface in `Services/`, implementation in `Services/Implementations/`, register in `ImmersiveEngine.ConfigureServices`.
- **Frontend actions**: Add new `BridgeAction` enum entry, handler in `AppBridge.HandleMessage` switch, callable from Vue via `bridge.yourAction(payload)`.
