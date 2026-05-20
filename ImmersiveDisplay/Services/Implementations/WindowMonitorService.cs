// File: Services/Implementations/WindowMonitorService.cs

using System;
using System.Threading;
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
        if (_locationHookHandle != IntPtr.Zero || _lifecycleHookHandle != IntPtr.Zero)
        {
            StopMonitoring();
        }

        _targetHwnd = hwnd;
        _currentMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            _loggingService.AddLog("[WindowMonitorService] Failed to get process ID. Cannot start monitoring.");
            return;
        }

        // Hook 1: Location / Size changes
        _locationHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        // Hook 2: Lifecycle events (Destroy/Hide)
        _lifecycleHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY,
            NativeMethods.EVENT_OBJECT_HIDE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_locationHookHandle != IntPtr.Zero && _lifecycleHookHandle != IntPtr.Zero)
        {
            _loggingService.AddLog($"[WindowMonitorService] Started monitoring HWND {hwnd}.");
        }
        else
        {
            StopMonitoring();
        }
    }

    public void StopMonitoring()
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
        
        _loggingService.AddLog("[WindowMonitorService] Stopped monitoring.");
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
