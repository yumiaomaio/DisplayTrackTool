using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

public enum WindowOrientation
{
    UNKNOWN,
    PORTRAIT,
    LANDSCAPE
}

public class TargetStateManager(
    IWindowMonitorService windowMonitor,
    IWindowLayoutManager layoutManager,
    IOverlayService overlayService,
    IConfigService configService,
    ILoggingService loggingService,
    ITaskbarService taskbarService,
    IDisplayService displayService,
    ILaunchService launchService)
    : ITargetStateManager, IDisposable
{
    // State
    private IntPtr _targetHwnd = IntPtr.Zero;
    private WindowOrientation _lastOrientation = WindowOrientation.UNKNOWN;
    private DisplayConfigRotation? _lastAppliedDisplayRotation;
    private CancellationTokenSource? _startCts;
    private readonly SemaphoreSlim _opLock = new(1, 1);
    private CancellationTokenSource? _runCts;

    public bool IsRunning
    {
        get => field;
        private set
        {
            if (field != value)
            {
                field = value;
                IsRunningChanged?.Invoke(value);
            }
        }
    }

    public IntPtr? CurrentTargetHwnd => _targetHwnd == IntPtr.Zero ? null : _targetHwnd;

    public int WaitingCountdown
    {
        get => field;
        private set
        {
            if (field != value)
            {
                field = value;
                WaitingCountdownChanged?.Invoke(value);
            }
        }
    }

    public event Action<bool>? IsRunningChanged;
    public event Action<int>? WaitingCountdownChanged;

    public async Task StartAsync(string processName, bool programAlreadyLaunched = false)
    {
        if (IsRunning)
        {
            AddLog("Already running. Please stop first.");
            return;
        }

        AddLog($"> StartAsync: processName='{processName}', programAlreadyLaunched={programAlreadyLaunched}, "
               + $"IsLaunchOnTaskStart={configService.IsLaunchOnTaskStartEnabled()}, "
               + $"HasLaunchPath={!string.IsNullOrWhiteSpace(configService.GetAssociatedLaunchPath())}");

        _startCts?.Cancel();
        _startCts?.Dispose();
        _startCts = new CancellationTokenSource();
        var token = _startCts.Token;

        await _opLock.WaitAsync(token);
        var lockHeld = true;
        try
        {
            AddLog($"Attempting to start for process: {processName}...");

            bool didLaunchAssociated = programAlreadyLaunched;

            // --- 1. 关联启动 ---
            if (configService.IsLaunchOnTaskStartEnabled())
            {
                var path = configService.GetAssociatedLaunchPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    launchService.Launch(path);
                    didLaunchAssociated = true;
                }
            }

            // --- 2. 首次瞬时探测 ---
            _targetHwnd = FindWindowByProcessName(processName);

            // --- 3. 结果判断与倒计时分流 ---
            if (_targetHwnd == IntPtr.Zero)
            {
                // 情况 A：没找到，而且本次也没有启动任何关联程序 -> 立即报错退出
                if (!didLaunchAssociated)
                {
                    WaitingCountdown = -1;
                    AddLog($"Error: Target process '{processName}' is not running. Start the process first.");
                    return;
                }

                // 情况 B：没找到，但刚才刚刚调起了关联程序 -> 进入倒计时等待
                _targetHwnd = await WaitForWindowAsync(processName, configService.GetWindowDetectionTimeout(), token);

                if (_targetHwnd == IntPtr.Zero)
                {
                    WaitingCountdown = -1;
                    AddLog($"Error: Could not find a visible window for process '{processName}' after launching.");
                    return;
                }
            }

            // 检查并还原最小化窗口
            if (NativeMethods.IsIconic(_targetHwnd))
            {
                AddLog("Target window is minimized. Restoring it to normal state before proceeding...");
                NativeMethods.ShowWindow(_targetHwnd, NativeMethods.SW_RESTORE);
            }

            AddLog($"Target window found: HWND {_targetHwnd}.");

            // --- 核心解耦：让服务自行备份状态 ---
            layoutManager.CaptureOriginalState(_targetHwnd);
            displayService.CaptureOriginalState(_targetHwnd);

            IsRunning = true;
            _lastOrientation = WindowOrientation.UNKNOWN;

            // --- 背景遮罩 ---
            if (configService.IsBackgroundOverlayEnabled())
            {
                AddLog("Background overlay is ENABLED. Showing overlay.");
                overlayService.Show(_targetHwnd);
            }

            // --- 任务栏控制 ---
            if (configService.IsTaskbarAutoHideEnabled())
            {
                taskbarService.CaptureOriginalState();
                taskbarService.SetAutoHide(true);
                AddLog("Taskbar auto-hide enabled.");
            }

            // --- 初始布局应用 ---
            AddLog("Applying initial portrait layout and monitor settings.");
            var profile = configService.GetPortraitProfile();
            try
            {
                await ApplyDisplayAndLayoutAsync(profile, token);
            }
            catch (Win32Exception ex)
            {
                AddLog(
                    $"[TargetStateManager] Failed to apply initial window layout: {ex.Message}. Exiting control process.");

                // 最小清理
                if (configService.IsBackgroundOverlayEnabled()) overlayService.Hide();
                if (configService.IsDisplaySyncEnabled() && _targetHwnd != IntPtr.Zero &&
                    NativeMethods.IsWindow(_targetHwnd))
                    displayService.RestoreOriginalState(_targetHwnd);
                if (configService.IsTaskbarAutoHideEnabled()) taskbarService.RestoreOriginalState();

                IsRunning = false;
                _targetHwnd = IntPtr.Zero;
                _opLock.Release();
                lockHeld = false;

                _ = Task.Run(() => NativeDialogHelper.ShowWarning(DialogKey.WindowStylePermission,
                    DialogKey.WindowStylePermissionTitle, ex.Message));

                return;
            }

            _lastOrientation = WindowOrientation.PORTRAIT;

            // --- 监控启动（在布局稳定后才开始监听）---
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            windowMonitor.WindowStateChanged += OnWindowStateChanged;
            windowMonitor.MonitorChanged += OnMonitorChanged;
            windowMonitor.WindowDestroyed += OnWindowDestroyed;
            windowMonitor.StartMonitoring(_targetHwnd);

            AddLog("Service started. Press F12 to stop.");
        }
        finally
        {
            if (lockHeld) _opLock.Release();
        }
    }

    public async Task StopAsync()
    {
        // 先发取消信号（不加锁），让 StartAsync 的倒计时循环能退出
        _startCts?.Cancel();
        _runCts?.Cancel();

        await _opLock.WaitAsync();
        try
        {
            if (!IsRunning)
            {
                _startCts?.Dispose();
                _startCts = null;
                _runCts?.Dispose();
                _runCts = null;
                return;
            }
            
            

            AddLog("Stopping service and restoring original states...");

            // 1. 停止监控
            windowMonitor.StopMonitoring();
            windowMonitor.WindowStateChanged -= OnWindowStateChanged;
            windowMonitor.MonitorChanged -= OnMonitorChanged;
            windowMonitor.WindowDestroyed -= OnWindowDestroyed;

            var lastHwnd = _targetHwnd;

            // 2. 依次还原各模块状态
            if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
            {
                layoutManager.RestoreOriginalState(lastHwnd);
            }

            if (configService.IsDisplaySyncEnabled())
            {
                if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
                    displayService.RestoreOriginalState(lastHwnd);
                await Task.Delay(500);
            }

            if (configService.IsTaskbarAutoHideEnabled())
            {
                taskbarService.RestoreOriginalState();
            }

            if (configService.IsBackgroundOverlayEnabled())
            {
                overlayService.Hide();
            }

            // 3. 清理状态
            _targetHwnd = IntPtr.Zero;
            IsRunning = false;
            _startCts?.Dispose();
            _startCts = null;
            _runCts?.Dispose();
            _runCts = null;

            AddLog("Service stopped.");
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async void OnWindowDestroyed(IntPtr hwnd)
    {
        try
        {
            if (hwnd == _targetHwnd && IsRunning)
            {
                AddLog("Target window was closed. Shutting down automatically.");
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error during automatic shutdown: {ex.Message}");
        }
    }

    private async void OnMonitorChanged(IntPtr hwnd, IntPtr hMonitor)
    {
        if (hwnd != _targetHwnd || !IsRunning) return;

        AddLog($"Window moved to a different monitor ({hMonitor}). Triggering automatic shutdown...");
        await StopAsync();
    }

    private async void OnWindowStateChanged(IntPtr hwnd, Rect newRect)
    {
        if (hwnd != _targetHwnd || !IsRunning) return;

        await _opLock.WaitAsync();
        var lockHeld = true;
        try
        {
            if (hwnd != _targetHwnd || !IsRunning) return;
            var currentOrientation = newRect.Width > newRect.Height
                ? WindowOrientation.LANDSCAPE
                : WindowOrientation.PORTRAIT;

            if (currentOrientation != _lastOrientation)
            {
                AddLog($"Orientation changed: {_lastOrientation} -> {currentOrientation}");
                _lastOrientation = currentOrientation;

                switch (currentOrientation)
                {
                    case WindowOrientation.PORTRAIT:
                        AddLog("Applying Portrait layout and monitor settings...");
                        await ApplyDisplayAndLayoutAsync(configService.GetPortraitProfile(), _runCts!.Token);
                        break;
                    case WindowOrientation.LANDSCAPE:
                        AddLog("Applying Landscape layout and monitor settings...");
                        await ApplyDisplayAndLayoutAsync(configService.GetLandscapeProfile(), _runCts!.Token);
                        break;
                }

                return;
            }

            // --- Topmost 状态维持 ---
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");

                if (_lastOrientation == WindowOrientation.PORTRAIT)
                {
                    var profile = configService.GetPortraitProfile();
                    layoutManager.ApplyLayout(_targetHwnd, profile);
                    _ = VerifyAndRetryLayoutAsync(_targetHwnd, profile, _runCts!.Token);
                }
                else if (_lastOrientation == WindowOrientation.LANDSCAPE)
                {
                    layoutManager.EnsureTopmost(_targetHwnd);
                }
            }
            else if (_lastAppliedDisplayRotation.HasValue)
            {
                var currentRotation = displayService.GetCurrentDisplayRotation(hwnd);
                if (currentRotation.HasValue && currentRotation.Value != _lastAppliedDisplayRotation.Value)
                {
                    bool matchesWindow = (_lastOrientation == WindowOrientation.PORTRAIT &&
                                          IsPortraitRotation(currentRotation.Value))
                                         || (_lastOrientation == WindowOrientation.LANDSCAPE &&
                                             IsLandscapeRotation(currentRotation.Value));

                    if (!matchesWindow)
                    {
                        AddLog(
                            $"Display rotation externally changed to {currentRotation.Value}, conflicting with {_lastOrientation} window. Shutting down.");
                        _opLock.Release();
                        lockHeld = false;
                        await StopAsync();
                    }
                }
            }
        }
        catch (Win32Exception ex)
        {
            AddLog($"[TargetStateManager] Win32 error in orientation change handler: {ex.Message}. Stopping service.");
            _opLock.Release();
            lockHeld = false;
            await StopAsync();
        }
        catch (Exception ex)
        {
            AddLog($"[TargetStateManager] Unexpected error in orientation change handler: {ex.Message}");
        }
        finally
        {
            if (lockHeld) _opLock.Release();
        }
    }

    /// <summary>
    /// Verifies if the window actually reached the expected size/position.
    /// If not (common with virtual displays or DPI changes), retries after a delay.
    /// </summary>
    private async Task VerifyAndRetryLayoutAsync(IntPtr hwnd, LayoutProfile profile, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        try
        {
            // Wait a bit for OS/drivers to settle
            await Task.Delay(300, token);

            if (token.IsCancellationRequested) return;

            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || !IsRunning) return;

            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                int currentW = rect.Right - rect.Left;
                int currentH = rect.Bottom - rect.Top;

                // Get target monitor info to see what the size should be
                IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    int targetW = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    int targetH = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;

                    // If deviation is more than a few pixels, retry
                    if (Math.Abs(currentW - targetW) > 5 || Math.Abs(currentH - targetH) > 5)
                    {
                        AddLog(
                            $"[TargetStateManager] Layout mismatch detected (Current: {currentW}x{currentH}, Target: {targetW}x{targetH}).");
                        AddLog("Retrying with AGGRESSIVE measures...");
                        layoutManager.ApplyAggressiveLayout(hwnd, profile);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by StopAsync — silently exit
        }
        catch (Win32Exception ex)
        {
            AddLog($"[TargetStateManager] Win32 error in verification retry task: {ex.Message}. Stopping service.");
            await StopAsync();
        }
        catch (Exception ex)
        {
            AddLog($"[TargetStateManager] Unexpected error in verification retry task: {ex.Message}");
        }
    }

    private static bool IsPortraitRotation(DisplayConfigRotation r) =>
        r is DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE90
            or DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE270;

    private static bool IsLandscapeRotation(DisplayConfigRotation r) =>
        r is DisplayConfigRotation.DISPLAYCONFIG_ROTATION_IDENTITY
            or DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE180;

    private async Task ApplyDisplayAndLayoutAsync(LayoutProfile profile, CancellationToken token)
    {
        if (configService.IsDisplaySyncEnabled())
        {
            displayService.ApplyDisplayProfile(_targetHwnd, profile.Display);
            if (profile.Display?.Orientation.HasValue == true)
                _lastAppliedDisplayRotation = DisplayService.MapToCcdRotation(profile.Display.Orientation.Value);
            await Task.Delay(500);
        }

        layoutManager.ApplyLayout(_targetHwnd, profile);
        _ = VerifyAndRetryLayoutAsync(_targetHwnd, profile, token);
    }

    private async Task<IntPtr> WaitForWindowAsync(string processName, int timeoutSeconds, CancellationToken token)
    {
        AddLog($"Waiting for launched target window to appear (up to {timeoutSeconds}s)...");

        for (int i = timeoutSeconds; i >= 0; i--)
        {
            if (token.IsCancellationRequested)
            {
                WaitingCountdown = 0;
                return IntPtr.Zero;
            }

            WaitingCountdown = i;

            var hwnd = await Task.Run(() => FindWindowByProcessName(processName));

            if (hwnd != IntPtr.Zero)
            {
                WaitingCountdown = 0;
                return hwnd;
            }

            if (i > 0)
            {
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    WaitingCountdown = 0;
                    return IntPtr.Zero;
                }
            }
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindWindowByProcessName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var process = processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        return process?.MainWindowHandle ?? IntPtr.Zero;
    }

    private void AddLog(string message)
    {
        loggingService.AddLog(message);
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}