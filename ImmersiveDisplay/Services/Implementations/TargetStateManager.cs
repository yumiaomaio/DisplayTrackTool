using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services.Components;

namespace ImmersiveDisplay.Services.Implementations;

public enum WindowOrientation
{
    Unknown,
    Portrait,
    Landscape
}

public class TargetStateManager(
    WindowMonitorService windowMonitor,
    WindowLayoutManager layoutManager,
    OverlayService overlayService,
    IConfigService configService,
    ILoggingService loggingService,
    TaskbarService taskbarService,
    DisplayService displayService,
    LaunchService launchService)
    : ITargetStateManager, IDisposable
{
    // State
    private IntPtr _targetHwnd = IntPtr.Zero;
    private WindowOrientation _lastOrientation = WindowOrientation.Unknown;
    private DisplayConfigRotation? _lastAppliedDisplayRotation;
    private CancellationTokenSource? _startCts;
    private readonly SemaphoreSlim _opLock = new(1, 1);
    private CancellationTokenSource? _runCts;
    private DateTime _lastMismatchTime = DateTime.MinValue;

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

            // --- 1. Launch associated program ---
            if (configService.IsLaunchOnTaskStartEnabled())
            {
                var path = configService.GetAssociatedLaunchPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    launchService.Launch(path);
                    didLaunchAssociated = true;
                }
            }

            // --- 2. First probe ---
            _targetHwnd = FindWindowByProcessName(processName);

            // --- 3. Decide result: error vs. countdown wait ---
            if (_targetHwnd == IntPtr.Zero)
            {
                // Case A: process not found and no program was launched — error out immediately
                if (!didLaunchAssociated)
                {
                    WaitingCountdown = -1;
                    AddLog($"Error: Target process '{processName}' is not running. Start the process first.");
                    return;
                }

                // Case B: process not found but we just launched it — wait with countdown
                _targetHwnd = await WaitForWindowAsync(processName, configService.GetWindowDetectionTimeout(), token);

                if (_targetHwnd == IntPtr.Zero)
                {
                    WaitingCountdown = -1;
                    AddLog($"Error: Could not find a visible window for process '{processName}' after launching.");
                    return;
                }
            }

            // Check and restore minimized window
            if (NativeMethods.IsIconic(_targetHwnd))
            {
                AddLog("Target window is minimized. Restoring it to normal state before proceeding...");
                NativeMethods.ShowWindow(_targetHwnd, NativeMethods.SW_RESTORE);
            }

            AddLog($"Target window found: HWND {_targetHwnd}.");

            // --- Decouple: services capture their own state ---
            layoutManager.CaptureOriginalState(_targetHwnd);
            displayService.CaptureOriginalState(_targetHwnd);

            IsRunning = true;
            _lastOrientation = WindowOrientation.Unknown;

            // --- Background overlay ---
            if (configService.IsBackgroundOverlayEnabled())
            {
                AddLog("Background overlay is ENABLED. Showing overlay.");
                overlayService.Show(_targetHwnd);
            }

            // --- Taskbar control ---
            if (configService.IsTaskbarAutoHideEnabled())
            {
                taskbarService.CaptureOriginalState();
                taskbarService.SetAutoHide(true);
                AddLog("Taskbar auto-hide enabled.");
            }
            
            MinimizeHostWindow();

            // --- Initial layout application ---
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

                RestoreHostWindow();
                // Minimal cleanup
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

            _lastOrientation = WindowOrientation.Portrait;

            // --- Start monitoring (only after layout is stable) ---
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
        // Cancel first (without lock) so StartAsync countdown loop can exit
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

            // 1. Stop monitoring
            windowMonitor.StopMonitoring();
            windowMonitor.WindowStateChanged -= OnWindowStateChanged;
            windowMonitor.MonitorChanged -= OnMonitorChanged;
            windowMonitor.WindowDestroyed -= OnWindowDestroyed;

            var lastHwnd = _targetHwnd;

            // 2. Restore each module state
            RestoreHostWindow();
            
            if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
            {
                layoutManager.RestoreOriginalState(lastHwnd);
            }

            if (configService.IsDisplaySyncEnabled())
            {
                if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
                    displayService.RestoreOriginalState(lastHwnd);
            }

            if (configService.IsTaskbarAutoHideEnabled())
            {
                taskbarService.RestoreOriginalState();
            }

            if (configService.IsBackgroundOverlayEnabled())
            {
                overlayService.Hide();
            }

            // 3. Clean up state
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
                ? WindowOrientation.Landscape
                : WindowOrientation.Portrait;

            if (currentOrientation != _lastOrientation)
            {
                AddLog($"Orientation changed: {_lastOrientation} -> {currentOrientation}");
                _lastOrientation = currentOrientation;

                switch (currentOrientation)
                {
                    case WindowOrientation.Portrait:
                        AddLog("Applying Portrait layout and monitor settings...");
                        await ApplyDisplayAndLayoutAsync(configService.GetPortraitProfile(), _runCts!.Token);
                        break;
                    case WindowOrientation.Landscape:
                        AddLog("Applying Landscape layout and monitor settings...");
                        await ApplyDisplayAndLayoutAsync(configService.GetLandscapeProfile(), _runCts!.Token);
                        break;
                }

                return;
            }

            // --- Topmost style maintenance ---
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");

                if (_lastOrientation == WindowOrientation.Portrait)
                {
                    var profile = configService.GetPortraitProfile();
                    layoutManager.ApplyLayout(_targetHwnd, profile);
                    _ = VerifyAndRetryLayoutAsync(_targetHwnd, profile, _runCts!.Token);
                }
                else if (_lastOrientation == WindowOrientation.Landscape)
                {
                    layoutManager.EnsureTopmost(_targetHwnd);
                }
            }
            else if (_lastAppliedDisplayRotation.HasValue)
            {
                var currentRotation = displayService.GetCurrentDisplayRotation(hwnd);
                if (currentRotation.HasValue && currentRotation.Value != _lastAppliedDisplayRotation.Value)
                {
                    bool matchesWindow = (_lastOrientation == WindowOrientation.Portrait &&
                                          IsPortraitRotation(currentRotation.Value))
                                         || (_lastOrientation == WindowOrientation.Landscape &&
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
            await Task.Delay(150, token);

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
                        var now = DateTime.UtcNow;
                        if ((now - _lastMismatchTime).TotalSeconds < 1)
                        {
                            AddLog(
                                "Rapid consecutive layout mismatches detected within 1s (display sync may be disabled). Stopping service.");
                            _ = Task.Run(() =>
                                NativeDialogHelper.ShowWarning(DialogKey.LayoutMismatch,
                                    DialogKey.LayoutMismatchTitle));
                            await StopAsync();
                            return;
                        }
                        _lastMismatchTime = now;
                        AddLog(
                            $"[TargetStateManager] Layout mismatch detected (Current: {currentW}x{currentH}, Target: {targetW}x{targetH}).");
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
        }
        
        if (configService.IsBackgroundOverlayEnabled()) 
            overlayService.UpdatePosition(_targetHwnd);
        
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

    private static void MinimizeHostWindow()
    {
        var hwnd = HostBridge.HostHwnd;
        if (hwnd != IntPtr.Zero)
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
    }

    private static void RestoreHostWindow()
    {
        var hwnd = HostBridge.HostHwnd;
        if (hwnd != IntPtr.Zero)
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
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