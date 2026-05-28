using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services.Components;

public class WindowMonitorService
{
    private readonly WindowThread _windowThread;
    private readonly ILoggingService _loggingService;

    private IntPtr _locationHookHandle = IntPtr.Zero;
    private IntPtr _lifecycleHookHandle = IntPtr.Zero;
    private readonly NativeMethods.WinEventDelegate _eventDelegate;
    private Timer? _debounceTimer;
    private readonly object _timerLock = new();
    private IntPtr _targetHwnd = IntPtr.Zero;
    private IntPtr _currentMonitor = IntPtr.Zero;

    public event Action<IntPtr>? WindowDestroyed;
    public event Action<IntPtr, Rect>? WindowStateChanged;
    public event Action<IntPtr, IntPtr>? MonitorChanged;

    public WindowMonitorService(WindowThread windowThread, ILoggingService loggingService)
    {
        _windowThread = windowThread;
        _loggingService = loggingService;
        _eventDelegate = WinEventProc;
    }

    public void StartMonitoring(IntPtr hwnd)
    {
        _windowThread.Post(() =>
        {
            if (_locationHookHandle != IntPtr.Zero || _lifecycleHookHandle != IntPtr.Zero)
                StopMonitoringInternal();

            _targetHwnd = hwnd;
            _currentMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                _loggingService.AddLog("[WindowMonitor] Failed to get process ID.");
                return;
            }

            _locationHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_DESTROY,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, _eventDelegate, processId, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_locationHookHandle != IntPtr.Zero)
                _loggingService.AddLog($"[WindowMonitor] Monitoring HWND {hwnd} (Process: {processId}).");
            else
                StopMonitoringInternal();
        });
    }

    public void StopMonitoring()
    {
        _windowThread.Post(() =>
        {
            StopMonitoringInternal();
            _loggingService.AddLog("[WindowMonitor] Stopped.");
        });
    }

    // --- Internal (runs on WindowThread) ---

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0 || hwnd != _targetHwnd) return;

        lock (_timerLock)
        {
            if (_debounceTimer == null)
                _debounceTimer = new Timer(DebounceTimerTick, null, 150, Timeout.Infinite);
            else
                _debounceTimer.Change(150, Timeout.Infinite);
        }
    }

    private void DebounceTimerTick(object? state)
    {
        _windowThread.Post(() =>
        {
            if (_targetHwnd == IntPtr.Zero) return;

            if (!NativeMethods.IsWindow(_targetHwnd) || !NativeMethods.IsWindowVisible(_targetHwnd))
            {
                _loggingService.AddLog($"[WindowMonitor] Window destroyed: HWND {_targetHwnd}.");
                WindowDestroyed?.Invoke(_targetHwnd);
                return;
            }

            var hMonitor = NativeMethods.MonitorFromWindow(_targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != _currentMonitor && hMonitor != IntPtr.Zero)
            {
                _loggingService.AddLog($"[WindowMonitor] Monitor change: {_currentMonitor} -> {hMonitor}");
                _currentMonitor = hMonitor;
                MonitorChanged?.Invoke(_targetHwnd, hMonitor);
            }

            if (NativeMethods.GetWindowRect(_targetHwnd, out var rect))
                WindowStateChanged?.Invoke(_targetHwnd, rect);
        });
    }

    private void StopMonitoringInternal()
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
        Timer? timer;
        lock (_timerLock) { timer = _debounceTimer; _debounceTimer = null; }
        timer?.Dispose();
    }
}
