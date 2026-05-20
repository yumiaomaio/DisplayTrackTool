using System.ComponentModel;
using System.Runtime.InteropServices;
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
    IWindowQueryService queryService,
    IWindowMonitorService monitorService,
    IWindowLayoutManager layoutManager,
    IOverlayService overlayService,
    IConfigService configService,
    ILoggingService loggingService,
    ITaskbarService taskbarService,
    IDisplayService displayService,
    ILaunchService launchService,
    IDialogService dialogService)
    : ITargetStateManager, IDisposable
{
    // State
    private IntPtr _targetHwnd = IntPtr.Zero;
    private WindowOrientation _lastOrientation = WindowOrientation.UNKNOWN;
    private CancellationTokenSource? _startCts;

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

    public async Task StartAsync(string processName)
    {
        if (IsRunning)
        {
            AddLog("Already running. Please stop first.");
            return;
        }

        if (_startCts != null)
        {
            _startCts.Cancel();
            _startCts.Dispose();
        }
        _startCts = new CancellationTokenSource();
        var token = _startCts.Token;

        AddLog($"Attempting to start for process: {processName}...");

        // --- 1. 关联启动 (优先执行) ---
        if (configService.IsLaunchOnTaskStartEnabled())
        {
            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                launchService.Launch(path);
            }
        }

        // --- 2. 窗口探测 (带倒计时轮询) ---
        _targetHwnd = IntPtr.Zero;
        int timeoutSeconds = configService.GetWindowDetectionTimeout();
        
        AddLog($"Waiting for target window to appear (up to {timeoutSeconds}s)...");
        
        for (int i = timeoutSeconds; i >= 0; i--)
        {
            if (token.IsCancellationRequested) 
            {
                WaitingCountdown = 0;
                return;
            }

            WaitingCountdown = i;
            
            _targetHwnd = await Task.Run(() => queryService.FindWindowByProcessName(processName) ?? IntPtr.Zero);
            
            if (_targetHwnd != IntPtr.Zero)
            {
                WaitingCountdown = 0;
                break;
            }

            if (i > 0)
            {
                try { await Task.Delay(1000, token); }
                catch (TaskCanceledException) { 
                    WaitingCountdown = 0;
                    return; 
                }
            }
        }

        if (_targetHwnd == IntPtr.Zero)
        {
            WaitingCountdown = -1; // Signal timeout/error
            AddLog($"Error: Could not find a visible window for process '{processName}' within {timeoutSeconds}s.");
            return;
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

        // --- 监控启动 ---
        monitorService.WindowStateChanged += OnWindowStateChanged;
        monitorService.MonitorChanged += OnMonitorChanged;
        monitorService.WindowDestroyed += OnWindowDestroyed;
        monitorService.StartMonitoring(_targetHwnd);

        // --- 初始布局应用 ---
        AddLog("Applying initial portrait layout and monitor settings.");
        var profile = configService.GetPortraitProfile();
        if (configService.IsDisplaySyncEnabled())
        {
            displayService.ApplyDisplayProfile(_targetHwnd, profile.Display);
            await Task.Delay(500); 
        }
        
        try
        {
            layoutManager.ApplyLayout(_targetHwnd, profile);
        }
        catch (Win32Exception ex)
        {
            AddLog($"[TargetStateManager] Failed to apply initial window layout: {ex.Message}. Exiting control process.");
            
            // 退出控制流程并执行清理
            await StopAsync();
            
            // 弹窗提示可能需要管理员权限
            dialogService.ShowWarning(
                $"""
                无法修改目标窗口样式。

                这通常是因为目标程序（游戏）是以管理员权限运行的，而本工具权限不足。

                请尝试【以管理员身份运行】本工具后再试。

                (错误信息: {ex.Message})
                """,
                "权限不足 / Privilege Elevation Required");
                
            return;
        }
        
        _lastOrientation = WindowOrientation.PORTRAIT;

        AddLog("Service started. Press F12 to stop.");
    }

    public async Task StopAsync()
    {
        if (_startCts != null)
        {
            _startCts.Cancel();
            _startCts.Dispose();
            _startCts = null;
        }
        
        if (!IsRunning) return;

        AddLog("Stopping service and restoring original states...");

        // 1. 停止监控
        monitorService.StopMonitoring();
        monitorService.WindowStateChanged -= OnWindowStateChanged;
        monitorService.MonitorChanged -= OnMonitorChanged;
        monitorService.WindowDestroyed -= OnWindowDestroyed;

        var lastHwnd = _targetHwnd;

        // 2. 依次还原各模块状态
        if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
        {
            layoutManager.RestoreOriginalState(lastHwnd);
        }

        if (configService.IsDisplaySyncEnabled())
        {
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

        AddLog("Service stopped.");
    }

    private async void OnWindowDestroyed(IntPtr hwnd)
    {
        try
        {
            if (hwnd == _targetHwnd)
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

        try
        {
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
                        var portraitProfile = configService.GetPortraitProfile();
                        if (configService.IsDisplaySyncEnabled())
                        {
                            displayService.ApplyDisplayProfile(_targetHwnd, portraitProfile.Display);
                            await Task.Delay(500);
                        }
                        layoutManager.ApplyLayout(_targetHwnd, portraitProfile);
                        _ = VerifyAndRetryLayoutAsync(_targetHwnd, portraitProfile);
                        break;
                    case WindowOrientation.LANDSCAPE:
                        AddLog("Applying Landscape layout and monitor settings...");
                        var landscapeProfile = configService.GetLandscapeProfile();
                        if (configService.IsDisplaySyncEnabled())
                        {
                            displayService.ApplyDisplayProfile(_targetHwnd, landscapeProfile.Display);
                            await Task.Delay(500);
                        }
                        layoutManager.ApplyLayout(_targetHwnd, landscapeProfile);
                        _ = VerifyAndRetryLayoutAsync(_targetHwnd, landscapeProfile);
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
                    _ = VerifyAndRetryLayoutAsync(_targetHwnd, profile);
                }
                else if (_lastOrientation == WindowOrientation.LANDSCAPE)
                {
                    layoutManager.EnsureTopmost(_targetHwnd);
                }
            }
        }
        catch (Win32Exception ex)
        {
            AddLog($"[TargetStateManager] Win32 error in orientation change handler: {ex.Message}. Stopping service.");
            await StopAsync();
        }
        catch (Exception ex)
        {
            AddLog($"[TargetStateManager] Unexpected error in orientation change handler: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies if the window actually reached the expected size/position.
    /// If not (common with virtual displays or DPI changes), retries after a delay.
    /// </summary>
    private async Task VerifyAndRetryLayoutAsync(IntPtr hwnd, LayoutProfile profile)
    {
        try
        {
            // Wait a bit for OS/drivers to settle
            await Task.Delay(300);

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
                        AddLog($"[TargetStateManager] Layout mismatch detected (Current: {currentW}x{currentH}, Target: {targetW}x{targetH}).");
                        
                        // --- Diagnostic Mode: Output detailed info ---
                        try 
                        {
                            var style = (WindowStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
                            var exStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                            var dpi = NativeMethods.GetDpiForWindow(hwnd);
                            
                            var placement = new NativeMethods.WINDOWPLACEMENT();
                            placement.length = Marshal.SizeOf(placement);
                            NativeMethods.GetWindowPlacement(hwnd, ref placement);

                            string diagnosticInfo = $"""
                                --- DEBUG DIAGNOSTIC ---
                                HWND: {hwnd.ToInt64()} (0x{hwnd.ToInt64():X})
                                Style: {style} (0x{(uint)style:X})
                                ExStyle: {exStyle} (0x{(uint)exStyle:X})
                                DPI: {dpi}
                                ShowCmd: {placement.showCmd}
                                Monitor WorkArea: {monitorInfo.rcWork.Left},{monitorInfo.rcWork.Top} - {monitorInfo.rcWork.Right},{monitorInfo.rcWork.Bottom}
                                """;
                            
                            AddLog(diagnosticInfo);
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[Diagnostic] Failed to gather details: {ex.Message}");
                        }

                        AddLog("Retrying with AGGRESSIVE measures...");
                        layoutManager.ApplyAggressiveLayout(hwnd, profile);
                    }
                }
            }
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

    private void AddLog(string message)
    {
        loggingService.AddLog(message);
    }

    private void AddLogs(params ReadOnlySpan<string> messages)
    {
        loggingService.AddLogs(messages);
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}
