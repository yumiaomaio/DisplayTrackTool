using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Views;

namespace ImmersiveDisplay;

/// <summary>
/// Manages a dedicated STA thread with a message pump for all HWND operations:
/// overlay window, keyboard hook, and WinEvent hooks.
/// Receives commands via Windows messages posted to its message-only HWND.
/// </summary>
public class OverlayHost : IDisposable
{
    private const string ClassName = "ImmersiveOverlayHost";
    private const uint WM_EXECUTE = NativeMethods.WM_USER + 0x1000;
    private const uint WM_OVERLAY_SHOW = NativeMethods.WM_USER + 0x1001;
    private const uint WM_OVERLAY_HIDE = NativeMethods.WM_USER + 0x1002;
    private const uint WM_OVERLAY_UPDATE = NativeMethods.WM_USER + 0x1003;

    private Thread? _thread;
    private IntPtr _hwnd = IntPtr.Zero;
    private readonly ManualResetEventSlim _hwndReady = new();
    private readonly ConcurrentQueue<Action> _actionQueue = new();
    private readonly NativeMethods.WndProc _wndProcDelegate;

    // Overlay window
    private OverlayWindowShell? _overlayWindow;

    // Keyboard hook
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

    // WinEvent hooks
    private IntPtr _locationHookHandle = IntPtr.Zero;
    private IntPtr _lifecycleHookHandle = IntPtr.Zero;
    private readonly NativeMethods.WinEventDelegate _eventDelegate;
    private Timer? _debounceTimer;
    private IntPtr _targetHwnd = IntPtr.Zero;
    private IntPtr _currentMonitor = IntPtr.Zero;

    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;

    // Events — receivers must not depend on thread affinity
    public event Action<int>? KeyPressed;
    public event Action<IntPtr, Rect>? WindowStateChanged;
    public event Action<IntPtr, IntPtr>? MonitorChanged;
    public event Action<IntPtr>? WindowDestroyed;

    public IntPtr Hwnd => _hwnd;
    public IntPtr OverlayHwnd => _overlayWindow?.Hwnd ?? IntPtr.Zero;

    public OverlayHost(IConfigService configService, ILoggingService loggingService)
    {
        _configService = configService;
        _loggingService = loggingService;
        _wndProcDelegate = WndProc;
        _hookProc = HookCallback;
        _eventDelegate = WinEventProc;
    }

    public void Start()
    {
        if (_thread != null) return;

        _thread = new Thread(ThreadProc)
        {
            Name = "OverlayHost",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _hwndReady.Wait();
    }

    public void Stop()
    {
        if (_thread == null) return;

        IntPtr hwnd = _hwnd;
        _thread = null;

        if (hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public void Post(Action action)
    {
        if (_hwnd == IntPtr.Zero) return;
        _actionQueue.Enqueue(action);
        NativeMethods.PostMessage(_hwnd, WM_EXECUTE, IntPtr.Zero, IntPtr.Zero);
    }

    public void ShowOverlay(IntPtr targetHwnd)
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(_hwnd, WM_OVERLAY_SHOW, targetHwnd, IntPtr.Zero);
    }

    public void HideOverlay()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(_hwnd, WM_OVERLAY_HIDE, IntPtr.Zero, IntPtr.Zero);
    }

    public void UpdateOverlay(IntPtr targetHwnd)
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(_hwnd, WM_OVERLAY_UPDATE, targetHwnd, IntPtr.Zero);
    }

    public void InstallKeyboardHook()
    {
        Post(InstallKeyboardHookInternal);
    }

    public void UninstallKeyboardHook()
    {
        Post(UninstallKeyboardHookInternal);
    }

    public void StartWindowMonitoring(IntPtr hwnd)
    {
        Post(() =>
        {
            if (_locationHookHandle != IntPtr.Zero || _lifecycleHookHandle != IntPtr.Zero)
                StopWindowMonitoringInternal();

            _targetHwnd = hwnd;
            _currentMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                _loggingService.AddLog("[OverlayHost] Failed to get process ID for monitoring.");
                return;
            }

            _locationHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_DESTROY,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, _eventDelegate, processId, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_locationHookHandle != IntPtr.Zero)
                _loggingService.AddLog($"[OverlayHost] Monitoring HWND {hwnd} (Process: {processId}).");
            else
                StopWindowMonitoringInternal();
        });
    }

    public void StopWindowMonitoring()
    {
        Post(() =>
        {
            StopWindowMonitoringInternal();
            _loggingService.AddLog("[OverlayHost] Window monitoring stopped.");
        });
    }

    // --- Thread lifecycle ---

    private void ThreadProc()
    {
        RegisterWindowClass();

        _hwnd = NativeMethods.CreateWindowEx(
            0, ClassName, "OverlayHostMsg",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null!),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            _loggingService.AddLog("[OverlayHost] Failed to create message window.");
            return;
        }

        _hwndReady.Set();

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(in msg);
            NativeMethods.DispatchMessage(in msg);
        }

        // Cleanup when message pump exits
        DisposeOverlayInternal();
        UninstallKeyboardHookInternal();
        StopWindowMonitoringInternal();

        _hwnd = IntPtr.Zero;
    }

    private void RegisterWindowClass()
    {
        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null!),
            lpszClassName = Marshal.StringToHGlobalUni(ClassName)
        };

        try
        {
            NativeMethods.RegisterClassEx(in wndClass);
        }
        finally
        {
            Marshal.FreeHGlobal(wndClass.lpszClassName);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_EXECUTE:
                ExecutePendingActions();
                return IntPtr.Zero;

            case WM_OVERLAY_SHOW:
                if (wParam != IntPtr.Zero)
                    ShowOverlayInternal(wParam);
                return IntPtr.Zero;

            case WM_OVERLAY_HIDE:
                DisposeOverlayInternal();
                return IntPtr.Zero;

            case WM_OVERLAY_UPDATE:
                if (_overlayWindow != null && wParam != IntPtr.Zero)
                {
                    DisposeOverlayInternal();
                    ShowOverlayInternal(wParam);
                }
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ExecutePendingActions()
    {
        while (_actionQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _loggingService.AddLog($"[OverlayHost] Execute error: {ex.Message}");
            }
        }
    }

    // --- Overlay window ---

    private void ShowOverlayInternal(IntPtr targetHwnd)
    {
        DisposeOverlayInternal();

        var backgroundMode = _configService.GetBackgroundMode();
        string? imagePath = null;
        string backgroundColor = "#FF000000";

        if (backgroundMode == BackgroundMode.IMAGE)
        {
            string? imageName = _configService.GetBackgroundImageFileName();
            if (!string.IsNullOrEmpty(imageName))
            {
                string fullPath = Path.Combine(AppContext.BaseDirectory, "Backgrounds", imageName);
                if (File.Exists(fullPath)) imagePath = fullPath;
            }
        }
        else
        {
            backgroundColor = _configService.GetBackgroundColor();
        }

        _overlayWindow = new OverlayWindowShell(imagePath, backgroundColor, _loggingService);

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };

        int x = 0, y = 0, width = 800, height = 600;

        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var mr = monitorInfo.rcMonitor;
            x = mr.Left; y = mr.Top;
            width = mr.Right - mr.Left;
            height = mr.Bottom - mr.Top;
        }

        _overlayWindow.Create(x, y, width, height);
        _overlayWindow.Show();

        // Sync Z-order with target window
        var targetExStyle = (WindowExStyles)NativeMethods.GetWindowLong(targetHwnd, NativeMethods.GWL_EXSTYLE);
        bool isTargetTopmost = targetExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST);

        NativeMethods.SetWindowPos(_overlayWindow.Hwnd,
            isTargetTopmost ? new IntPtr(-1) : new IntPtr(-2),
            0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

        NativeMethods.SetWindowPos(_overlayWindow.Hwnd, targetHwnd, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

        _loggingService.AddLog("[OverlayHost] Overlay shown.");
    }

    private void DisposeOverlayInternal()
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose();
            _overlayWindow = null;
            _loggingService.AddLog("[OverlayHost] Overlay hidden.");
        }
    }

    // --- Keyboard hook ---

    private void InstallKeyboardHookInternal()
    {
        if (_hookId != IntPtr.Zero) return;

        try
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule!;
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _hookProc,
                NativeMethods.GetModuleHandle(curModule.ModuleName),
                0);

            if (_hookId == IntPtr.Zero)
                _loggingService.AddLog("[OverlayHost] Keyboard hook failed. Error: " + Marshal.GetLastWin32Error());
            else
                _loggingService.AddLog("[OverlayHost] Keyboard hook installed.");
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[OverlayHost] Exception installing keyboard hook: {ex.Message}");
        }
    }

    private void UninstallKeyboardHookInternal()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            try
            {
                var kbdStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
                KeyPressed?.Invoke((int)kbdStruct.vkCode);
            }
            catch (Exception ex)
            {
                _loggingService.AddLog($"[OverlayHost] Hook callback error: {ex.Message}");
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    // --- WinEvent monitoring ---

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0 || hwnd != _targetHwnd) return;

        if (_debounceTimer == null)
            _debounceTimer = new Timer(DebounceTimerTick, null, 150, Timeout.Infinite);
        else
            _debounceTimer.Change(150, Timeout.Infinite);
    }

    private void DebounceTimerTick(object? state)
    {
        Post(() =>
        {
            if (_targetHwnd == IntPtr.Zero) return;

            if (!NativeMethods.IsWindow(_targetHwnd) || !NativeMethods.IsWindowVisible(_targetHwnd))
            {
                _loggingService.AddLog($"[OverlayHost] Window terminal state for HWND {_targetHwnd}.");
                WindowDestroyed?.Invoke(_targetHwnd);
                return;
            }

            IntPtr hMonitor = NativeMethods.MonitorFromWindow(_targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != _currentMonitor && hMonitor != IntPtr.Zero)
            {
                _loggingService.AddLog($"[OverlayHost] Monitor change: {_currentMonitor} -> {hMonitor}");
                _currentMonitor = hMonitor;
                MonitorChanged?.Invoke(_targetHwnd, hMonitor);
            }

            if (NativeMethods.GetWindowRect(_targetHwnd, out var rect))
                WindowStateChanged?.Invoke(_targetHwnd, rect);
        });
    }

    private void StopWindowMonitoringInternal()
    {
        if (_locationHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_locationHookHandle);
            _locationHookHandle = IntPtr.Zero;
        }
        if (_lifecycleHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_lifecycleHookHandle);
            _lifecycleHookHandle = IntPtr.Zero;
        }
        _targetHwnd = IntPtr.Zero;
        _currentMonitor = IntPtr.Zero;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
