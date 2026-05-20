// File: Services/Implementations/WindowMonitorService.cs

using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services.Implementations;

public class WindowMonitorService : IWindowMonitorService, IDisposable
{
    public event Action<IntPtr, Rect>? WindowStateChanged;
    public event Action<IntPtr, IntPtr>? MonitorChanged;
    public event Action<IntPtr>? WindowDestroyed;

    private IntPtr _locationHookHandle = IntPtr.Zero;
    private IntPtr _lifecycleHookHandle = IntPtr.Zero;
    private IntPtr _currentMonitor = IntPtr.Zero;

    private readonly NativeMethods.WinEventDelegate _eventDelegate;
    private Timer? _debounceTimer;
    private IntPtr _targetHwnd;
    private readonly ILoggingService _loggingService;

    public WindowMonitorService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _eventDelegate = WinEventProc;
    }

    public void StartMonitoring(IntPtr hwnd)
    {
        UiDispatcher.BeginInvoke(() =>
        {
            if (_locationHookHandle != IntPtr.Zero || _lifecycleHookHandle != IntPtr.Zero)
            {
                StopMonitoringInternal();
            }

            _targetHwnd = hwnd;
            _currentMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                _loggingService.AddLog("[WindowMonitorService] Failed to get process ID. Cannot start monitoring.");
                return;
            }

            // Broad Hook: From DESTROY (0x8001) to LOCATIONCHANGE (0x800B)
            // This covers Show, Hide, Reorder (Z-Order), StateChange (Style), and LocationChange.
            // We use threadId = 0 to monitor all threads in the target process.
            _locationHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_DESTROY,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, _eventDelegate, processId, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_locationHookHandle != IntPtr.Zero)
            {
                _loggingService.AddLog($"[WindowMonitorService] Started monitoring HWND {hwnd} (Process: {processId}).");
            }
            else
            {
                StopMonitoringInternal();
            }
        });
    }

    public void StopMonitoring()
    {
        UiDispatcher.BeginInvoke(() =>
        {
            StopMonitoringInternal();
            _loggingService.AddLog("[WindowMonitorService] Stopped monitoring.");
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
        
        if (_debounceTimer != null)
        {
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0 || hwnd != _targetHwnd) return;

        // Restart debounce timer
        if (_debounceTimer == null)
        {
            _debounceTimer = new Timer(DebounceTimer_Tick, null, 150, Timeout.Infinite);
        }
        else
        {
            _debounceTimer.Change(150, Timeout.Infinite);
        }
    }

    private void DebounceTimer_Tick(object? state)
    {
        UiDispatcher.BeginInvoke(() =>
        {
            IntPtr hwnd = _targetHwnd;
            if (hwnd == IntPtr.Zero) return;

            // 1. Verify window exists and is visible
            if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
            {
                _loggingService.AddLog($"[WindowMonitorService] Window terminal state confirmed for HWND {hwnd}.");
                WindowDestroyed?.Invoke(hwnd);
                return;
            }

            // 2. Check monitor switch
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != _currentMonitor && hMonitor != IntPtr.Zero)
            {
                _loggingService.AddLog($"[WindowMonitorService] Monitor change confirmed: {_currentMonitor} -> {hMonitor}");
                _currentMonitor = hMonitor;
                MonitorChanged?.Invoke(hwnd, hMonitor);
            }

            // 3. Check position/size changes
            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                _loggingService.AddLog($"[WindowMonitorService] Window state update confirmed for HWND {hwnd}. New Rect: L={rect.Left}, T={rect.Top}, W={rect.Width}, H={rect.Height}");
                WindowStateChanged?.Invoke(hwnd, rect);
            }
        });
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
