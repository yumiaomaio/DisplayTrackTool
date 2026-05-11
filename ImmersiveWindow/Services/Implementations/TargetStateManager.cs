// File: Services/Implementations/TargetStateManager.cs

using System.Windows;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Enums;
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
    ITaskbarService taskbarService)
    : ITargetStateManager, IDisposable
{
    // Injected Services

    // State
    private IntPtr _targetHwnd = IntPtr.Zero;
    private WindowOrientation _lastOrientation = WindowOrientation.UNKNOWN;
    private bool _isRunning = false;
    private WindowSnapshot? _originalSnapshot;
    private bool _originalTaskbarAutoHide;

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

        // 备份原始状态
        _originalSnapshot = layoutManager.TakeSnapshot(_targetHwnd);
        AddLog("Original window styles and position backed up.");

        _isRunning = true;
        _lastOrientation = WindowOrientation.UNKNOWN;

        // --- 背景遮罩开关 ---
        if (configService.IsBackgroundOverlayEnabled())
        {
            AddLog("Background overlay is ENABLED. Showing overlay.");
            overlayService.Show(_targetHwnd);
        }

        // --- 任务栏自动隐藏集成 ---
        if (configService.IsTaskbarAutoHideEnabled())
        {
            _originalTaskbarAutoHide = taskbarService.IsAutoHideEnabled();
            if (!_originalTaskbarAutoHide)
            {
                AddLog("Enabling Taskbar auto-hide...");
                taskbarService.SetAutoHide(true);
            }
        }

        monitorService.WindowStateChanged += OnWindowStateChanged;
        monitorService.WindowDestroyed += OnWindowDestroyed;
        monitorService.StartMonitoring(_targetHwnd);

        keyboardHookService.KeyPressed += OnKeyPressed;
        keyboardHookService.Start();

        AddLog("Applying initial portrait layout for the window.");
        layoutManager.ApplyLayout(_targetHwnd, configService.GetPortraitProfile());
        _lastOrientation = WindowOrientation.PORTRAIT;

        IsRunningChanged?.Invoke(_isRunning);
        AddLog("Service started. Press F12 to stop.");
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        AddLog("Stopping service...");

        keyboardHookService.Stop();
        keyboardHookService.KeyPressed -= OnKeyPressed;
        monitorService.StopMonitoring();
        monitorService.WindowStateChanged -= OnWindowStateChanged;
        monitorService.WindowDestroyed -= OnWindowDestroyed;

        if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindow(_targetHwnd))
        {
            if (_originalSnapshot != null)
            {
                AddLog("Restoring target window to original styles and position.");
                await Task.Run(() => layoutManager.Restore(_targetHwnd, _originalSnapshot));
            }
        }

        // 还原任务栏状态
        if (configService.IsTaskbarAutoHideEnabled() && !_originalTaskbarAutoHide)
        {
            AddLog("Restoring Taskbar auto-hide state...");
            taskbarService.SetAutoHide(false);
        }

        if (configService.IsBackgroundOverlayEnabled())
        {
            overlayService.Hide();
        }

        _targetHwnd = IntPtr.Zero;
        _originalSnapshot = null;
        _isRunning = false;

        IsRunningChanged?.Invoke(_isRunning);

        AddLog("Service stopped.");
    }

    private async void OnKeyPressed(int vkCode)
    {
        try
        {
            const int vkF12 = 0x7B; // F12的虚拟键码

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

    private void OnWindowStateChanged(IntPtr hwnd, Rect newRect)
    {
        if (hwnd != _targetHwnd || !_isRunning) return;

        // --- 1. 方向改变检测 ---
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
                    AddLog("Applying Portrait layout...");
                    layoutManager.ApplyLayout(_targetHwnd, configService.GetPortraitProfile());
                    break;
                case WindowOrientation.LANDSCAPE:
                    AddLog("Applying Landscape layout...");
                    layoutManager.ApplyLayout(_targetHwnd, configService.GetLandscapeProfile());
                    break;
            }

            return;
        }

        // --- 2. Topmost 状态维持 (如果方向没有改变) ---
        var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
        {
            AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");

            if (_lastOrientation == WindowOrientation.PORTRAIT)
            {
                AddLog("Re-applying full Portrait layout to ensure consistency.");
                layoutManager.ApplyLayout(_targetHwnd, configService.GetPortraitProfile());
            }
            else if (_lastOrientation == WindowOrientation.LANDSCAPE)
            {
                AddLog("Patching Topmost style for Landscape mode.");
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