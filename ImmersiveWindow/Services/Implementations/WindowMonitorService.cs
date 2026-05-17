// File: Services/Implementations/WindowMonitorService.cs

using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Enums;

namespace ImmersiveWindow.Services.Implementations;

public class WindowMonitorService : IWindowMonitorService, IDisposable
{
    public event Action<IntPtr, Rect>? WindowStateChanged;
    public event Action<IntPtr, IntPtr>? MonitorChanged;
    public event Action<IntPtr>? WindowDestroyed;

    private IntPtr _locationHookHandle = IntPtr.Zero;
    private IntPtr _lifecycleHookHandle = IntPtr.Zero;
    private IntPtr _currentMonitor = IntPtr.Zero;

    private readonly NativeMethods.WinEventDelegate _eventDelegate;
    private readonly DispatcherTimer _debounceTimer;
    private IntPtr _targetHwnd;

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

        _targetHwnd = hwnd;
        _currentMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);

        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            Debug.WriteLine("[WindowMonitorService] Failed to get process ID. Cannot start monitoring.");
            return;
        }

        // 钩子1: 专门用于位置/大小变化
        _locationHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        // 钩子2: 生命周期事件 (Hide/Destroy)
        _lifecycleHookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY,
            NativeMethods.EVENT_OBJECT_HIDE,
            IntPtr.Zero, _eventDelegate, processId, threadId, NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_locationHookHandle != IntPtr.Zero && _lifecycleHookHandle != IntPtr.Zero)
        {
            Debug.WriteLine($"[WindowMonitorService] Started monitoring HWND {hwnd}.");
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
        _debounceTimer.Stop();
        Debug.WriteLine("[WindowMonitorService] Stopped monitoring.");
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        // 仅处理我们关注的窗口，且排除非窗口对象的干扰
        if (idObject != 0 || hwnd != _targetHwnd) return;

        // 无论是移动还是销毁，都重新启动去抖计时器
        // 这样可以确保在一连串动作（比如窗口先缩放再关闭）完成后，只执行最后一次状态判定
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();

        IntPtr hwnd = _targetHwnd;
        if (hwnd == IntPtr.Zero) return;

        // --- 1. 验证窗口是否还存在且可见 ---
        // 这是对 Hook 2 的去抖验证。如果 150ms 后窗口已经没了或隐藏了，触发销毁事件。
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
        {
            Debug.WriteLine($"[WindowMonitorService] Window terminal state confirmed for HWND {hwnd}.");
            WindowDestroyed?.Invoke(hwnd);
            return; // 窗口都没了，不需要后续位置检测了
        }

        // --- 2. 检查显示器切换 ---
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        if (hMonitor != _currentMonitor && hMonitor != IntPtr.Zero)
        {
            Debug.WriteLine($"[WindowMonitorService] Monitor change confirmed: {_currentMonitor} -> {hMonitor}");
            _currentMonitor = hMonitor;
            MonitorChanged?.Invoke(hwnd, hMonitor);
        }

        // --- 3. 检查位置/大小变化 ---
        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            var windowsRect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            Debug.WriteLine($"[WindowMonitorService] Window state update confirmed for HWND {hwnd}. New Rect: {windowsRect}");
            WindowStateChanged?.Invoke(hwnd, windowsRect);
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
