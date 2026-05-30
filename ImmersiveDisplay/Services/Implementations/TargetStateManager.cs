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

        // Phase 1: cancel previous run, create fresh token
        _startCts?.Cancel();
        _startCts?.Dispose();
        _startCts = new CancellationTokenSource();
        var token = _startCts.Token;

        await _opLock.WaitAsync(token);
        var lockHeld = true;
        try
        {
            AddLog($"Attempting to start for process: {processName}...");

            // Phase 2: launch associated program (fire-and-forget)
            var path = configService.GetAssociatedLaunchPath();
            var shouldLaunch = configService.IsLaunchOnTaskStartEnabled() && !string.IsNullOrWhiteSpace(path);
            if (shouldLaunch) _ = launchService.LaunchAsync(path!);

            // Phase 3: first window probe
            _targetHwnd = FindWindowByProcessName(processName);

            // Phase 4: nothing launched + not running → error
            var didLaunch = shouldLaunch || programAlreadyLaunched;
            if (!didLaunch && _targetHwnd == IntPtr.Zero)
            {
                WaitingCountdown = -1;
                AddLog($"Error: Target process '{processName}' is not running. Start the process first.");
                return;
            }

            // Phase 5: wait loop — window not yet visible after launch
            if (_targetHwnd == IntPtr.Zero)
            {
                var remaining = configService.GetWindowDetectionTimeout();
                AddLog($"Waiting for launched target window to appear (up to {remaining}s)...");
                try
                {
                    while (remaining > 0)
                    {
                        if (token.IsCancellationRequested) break;

                        WaitingCountdown = remaining;
                        _targetHwnd = FindWindowByProcessName(processName);
                        if (_targetHwnd != IntPtr.Zero) break;

                        if (remaining <= 1) break;
                        await Task.Delay(1000, token);
                        remaining--;
                    }
                }
                catch (OperationCanceledException)
                {
                    // frontend cancel or StopAsync cancelled the token
                    WaitingCountdown = 0;
                }
            }

            // Phase 6: timeout or cancelled — window never appeared
            if (_targetHwnd == IntPtr.Zero)
            {
                WaitingCountdown = -1;
                AddLog($"Error: Could not find a visible window for process '{processName}' after launching.");
                return;
            }

            // Phase 7: restore minimized window
            if (NativeMethods.IsIconic(_targetHwnd))
            {
                AddLog("Target window is minimized. Restoring it to normal state before proceeding...");
                NativeMethods.ShowWindow(_targetHwnd, NativeMethods.SW_RESTORE);
            }

            AddLog($"Target window found: HWND {_targetHwnd}.");

            // Phase 8: capture original window + display state (for later restore)
            layoutManager.CaptureOriginalState(_targetHwnd);
            displayService.CaptureOriginalState(_targetHwnd);

            IsRunning = true;
            _lastOrientation = WindowOrientation.Unknown;

            // Phase 9: show background overlay
            if (configService.IsBackgroundOverlayEnabled())
            {
                AddLog("Background overlay is ENABLED. Showing overlay.");
                overlayService.Show(_targetHwnd);
            }

            // Phase 10: auto-hide taskbar
            if (configService.IsTaskbarAutoHideEnabled())
            {
                taskbarService.CaptureOriginalState();
                taskbarService.SetAutoHide(true);
                AddLog("Taskbar auto-hide enabled.");
            }

            MinimizeHostWindow();

            // Phase 11: apply initial portrait layout + display rotation
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

                RestoreServices(_targetHwnd);

                IsRunning = false;
                _targetHwnd = IntPtr.Zero;
                _opLock.Release();
                lockHeld = false;

                _ = Task.Run(() => NativeDialogHelper.ShowWarning(DialogKey.WindowStylePermission,
                    DialogKey.WindowStylePermissionTitle, ex.Message));

                return;
            }

            _lastOrientation = WindowOrientation.Portrait;

            // Phase 12: subscribe window events, start monitoring loop
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
        // Phase 1: cancel tokens before acquiring lock, so StartAsync wait loop exits
        _startCts?.Cancel();
        _runCts?.Cancel();

        await _opLock.WaitAsync();
        try
        {
            if (!IsRunning)
            {
                CleanupCancellationTokens();
                return;
            }

            AddLog("Stopping service and restoring original states...");

            // Phase 2: unsubscribe window events, stop monitoring
            windowMonitor.StopMonitoring();
            windowMonitor.WindowStateChanged -= OnWindowStateChanged;
            windowMonitor.MonitorChanged -= OnMonitorChanged;
            windowMonitor.WindowDestroyed -= OnWindowDestroyed;

            var lastHwnd = _targetHwnd;

            // Phase 3: restore window layout, display, taskbar, overlay
            RestoreServices(lastHwnd);
            if (lastHwnd != IntPtr.Zero && NativeMethods.IsWindow(lastHwnd))
                layoutManager.RestoreOriginalState(lastHwnd);

            // Phase 4: reset state, dispose tokens
            _targetHwnd = IntPtr.Zero;
            IsRunning = false;
            CleanupCancellationTokens();

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

            var layoutProfile = currentOrientation == WindowOrientation.Portrait
                ? configService.GetPortraitProfile()
                : configService.GetLandscapeProfile();

            if (currentOrientation != _lastOrientation)
            {
                // orientation flipped → re-apply layout for new direction
                AddLog($"Orientation changed: {_lastOrientation} -> {currentOrientation}");
                _lastOrientation = currentOrientation;
                await ApplyDisplayAndLayoutAsync(layoutProfile, _runCts!.Token);
                return;
            }

            // topmost style maintenance: other apps may strip WS_EX_TOPMOST
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");
                layoutManager.ApplyLayout(_targetHwnd, layoutProfile);
                _ = VerifyAndRetryLayoutAsync(_targetHwnd, layoutProfile, _runCts!.Token);
            }

            if (!_lastAppliedDisplayRotation.HasValue) return;

            // rotation guard: if display rotation was externally changed to conflict
            var currentRotation = displayService.GetCurrentDisplayRotation(hwnd);
            if (currentRotation.HasValue && currentRotation.Value != _lastAppliedDisplayRotation.Value)
            {
                var isPortraitRotation = currentRotation.Value
                    is DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE90
                    or DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE270;
                var isLandscapeRotation = currentRotation.Value
                    is DisplayConfigRotation.DISPLAYCONFIG_ROTATION_IDENTITY
                    or DisplayConfigRotation.DISPLAYCONFIG_ROTATION_ROTATE180;

                var matchesWindow = (_lastOrientation == WindowOrientation.Portrait && isPortraitRotation)
                                    || (_lastOrientation == WindowOrientation.Landscape && isLandscapeRotation);
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

            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;

            int currentW = rect.Right - rect.Left;
            int currentH = rect.Bottom - rect.Top;

            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

            int targetW = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
            int targetH = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;

            if (Math.Abs(currentW - targetW) <= 5 && Math.Abs(currentH - targetH) <= 5) return;

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

    private Task ApplyDisplayAndLayoutAsync(LayoutProfile profile, CancellationToken token)
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

        return Task.CompletedTask;
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

    private void CleanupCancellationTokens()
    {
        _startCts?.Dispose();
        _startCts = null;
        _runCts?.Dispose();
        _runCts = null;
    }

    private void RestoreServices(IntPtr hwnd)
    {
        RestoreHostWindow();
        if (configService.IsBackgroundOverlayEnabled()) overlayService.Hide();
        if (configService.IsDisplaySyncEnabled() && hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
            displayService.RestoreOriginalState(hwnd);
        if (configService.IsTaskbarAutoHideEnabled()) taskbarService.RestoreOriginalState();
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