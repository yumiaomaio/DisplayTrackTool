// File: Services/Implementations/TargetStateManager.cs

using System.Runtime.InteropServices;
using System.Windows;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Enums;
using ImmersiveWindow.Interop.Structs;
using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services.Implementations;

public class TargetStateManager(
    IWindowQueryService queryService,
    IWindowMonitorService monitorService,
    IWindowLayoutManager layoutManager,
    IOverlayService overlayService,
    IConfigService configService,
    IKeyboardHookService keyboardHookService,
    ILoggingService loggingService,
    ITaskbarService taskbarService,
    IDisplayService displayService)
    : ITargetStateManager, IDisposable
{
    // State
    private IntPtr _targetHwnd = IntPtr.Zero;
    private WindowOrientation _lastOrientation = WindowOrientation.UNKNOWN;
    private bool _isRunning = false;

    public event Action<bool>? IsRunningChanged;

    public async Task StartAsync(string processName)
    {
        if (_isRunning)
        {
            AddLog("Already running. Please stop first.");
            return;
        }

        AddLog($"Attempting to start for process: {processName}...");

        _targetHwnd = await Task.Run(() => queryService.FindWindowByProcessName(processName) ?? IntPtr.Zero);

        if (_targetHwnd == IntPtr.Zero)
        {
            AddLog($"Error: Could not find a visible window for process '{processName}'.");
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

        _isRunning = true;
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

        keyboardHookService.Start();
        keyboardHookService.KeyPressed += OnKeyPressed;

        // --- 初始布局应用 ---
        AddLog("Applying initial portrait layout and monitor settings.");
        var profile = configService.GetPortraitProfile();
        if (configService.IsDisplaySyncEnabled())
        {
            displayService.ApplyDisplayProfile(_targetHwnd, profile.Display);
            await Task.Delay(500); 
        }
        layoutManager.ApplyLayout(_targetHwnd, profile);
        _lastOrientation = WindowOrientation.PORTRAIT;

        IsRunningChanged?.Invoke(_isRunning);
        AddLog("Service started. Press F12 to stop.");
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        AddLog("Stopping service and restoring original states...");

        // 1. 停止监控和钩子
        keyboardHookService.Stop();
        keyboardHookService.KeyPressed -= OnKeyPressed;
        monitorService.StopMonitoring();
        monitorService.WindowStateChanged -= OnWindowStateChanged;
        monitorService.MonitorChanged -= OnMonitorChanged;
        monitorService.WindowDestroyed -= OnWindowDestroyed;

        var lastHwnd = _targetHwnd;

        // 2. 依次还原各模块状态
        if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
        {
            layoutManager.RestoreOriginalState(lastHwnd);
            
            if (configService.IsDisplaySyncEnabled())
            {
                displayService.RestoreOriginalState(lastHwnd);
                await Task.Delay(500);
            }
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
        _isRunning = false;

        IsRunningChanged?.Invoke(_isRunning);
        AddLog("Service stopped.");
    }

    private async void OnKeyPressed(int vkCode)
    {
        try
        {
            const int vkF12 = 0x7B; 

            if (vkCode == vkF12)
            {
                AddLog("F12 key pressed. Shutting down...");
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error during F12 shutdown: {ex.Message}");
        }
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
        if (hwnd != _targetHwnd || !_isRunning) return;

        AddLog($"Window moved to a different monitor ({hMonitor}). Triggering automatic shutdown...");
        await StopAsync();
    }

    private async void OnWindowStateChanged(IntPtr hwnd, System.Windows.Rect newRect)
    {
        if (hwnd != _targetHwnd || !_isRunning) return;

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
                layoutManager.ApplyLayout(_targetHwnd, configService.GetPortraitProfile());
            }
            else if (_lastOrientation == WindowOrientation.LANDSCAPE)
            {
                layoutManager.EnsureTopmost(_targetHwnd);
            }
        }
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
