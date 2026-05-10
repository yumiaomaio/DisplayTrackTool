// File: Services/Implementations/WindowMonitorService.cs

using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ResponsiveWindowTool.Interop;

namespace ResponsiveWindowTool.Services.Implementations;

public class WindowMonitorService : IWindowMonitorService, IDisposable
{
    public event Action<IntPtr, Rect>? WindowStateChanged;
    public event Action<IntPtr>? WindowDestroyed;

    private IntPtr _locationHookHandle = IntPtr.Zero; // <-- 钩子句柄1
    private IntPtr _lifecycleHookHandle = IntPtr.Zero; // <-- 钩子句柄2

    private readonly NativeMethods.WinEventDelegate _eventDelegate;
    private readonly DispatcherTimer _debounceTimer;
    private IntPtr _lastEventHwnd;

    public WindowMonitorService()
    {
        _eventDelegate = WinEventProc;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    public void StartMonitoring(IntPtr hwnd)
    {
        if (_locationHookHandle != IntPtr.Zero || _lifecycleHookHandle != IntPtr.Zero)
        {
            StopMonitoring();
        }

        uint processId;
        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
        if (processId == 0)
        {
            Debug.WriteLine("[WindowMonitorService] Failed to get process ID. Cannot start monitoring.");
            return;
        }

        // 钩子1: 专门用于位置/大小变化，需要去抖动
        _locationHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        // 钩子2: 专门用于生命周期事件 (Hide/Destroy)，不需要去抖动
        // 事件范围从 DESTROY (0x8001) 到 HIDE (0x8003) 是有效的
        _lifecycleHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY,
            NativeMethods.EVENT_OBJECT_HIDE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_locationHookHandle != IntPtr.Zero && _lifecycleHookHandle != IntPtr.Zero)
        {
            Debug.WriteLine($"[WindowMonitorService] Started monitoring HWND {hwnd} with two hooks.");
        }
        else
        {
            Debug.WriteLine("[WindowMonitorService] Failed to set one or more hooks.");
            StopMonitoring(); // 如果有一个失败了，就清理掉所有
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

        _debounceTimer.Stop();
        Debug.WriteLine("[WindowMonitorService] Stopped monitoring.");
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0 || hwnd == IntPtr.Zero) return;

        switch (eventType)
        {
            // 生命周期事件
            case NativeMethods.EVENT_OBJECT_DESTROY:
            case NativeMethods.EVENT_OBJECT_HIDE:
                Debug.WriteLine(
                    $"[WindowMonitorService] Received lifecycle event 0x{eventType:X} for HWND {hwnd}.");
                WindowDestroyed?.Invoke(hwnd); // 将 HIDE 也视为销毁信号
                break;

            // 位置/大小变化事件
            case NativeMethods.EVENT_OBJECT_LOCATIONCHANGE:
                _lastEventHwnd = hwnd;
                _debounceTimer.Stop();
                _debounceTimer.Start();
                break;
        }
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();

        IntPtr hwnd = _lastEventHwnd;
        if (hwnd == IntPtr.Zero) return;

        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            var windowsRect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            Debug.WriteLine($"[WindowMonitorService] Debounced event for HWND {hwnd}. New Rect: {windowsRect}");
            WindowStateChanged?.Invoke(hwnd, windowsRect);
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}