// File: Services/Implementations/OverlayService.cs

using System.Runtime.InteropServices;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Views;

namespace ImmersiveDisplay.Services.Implementations;

public class OverlayService : IOverlayService, IDisposable
{
    private OverlayWindowShell? _overlayWindow;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private IntPtr _lastTargetHwnd = IntPtr.Zero;

    public IntPtr? WindowHandle => _overlayWindow?.Hwnd;

    public OverlayService(IConfigService configService, ILoggingService loggingService)
    {
        _configService = configService;
        _loggingService = loggingService;
        
        _configService.ConfigChanged += OnConfigChanged;
    }

    private void OnConfigChanged(string key, object? value)
    {
        if (_lastTargetHwnd == IntPtr.Zero) return;

        if (key == "EnableBackgroundOverlay")
        {
            bool enabled = value is bool b && b;
            _loggingService.AddLog($"[OverlayService] Toggle Overlay: {enabled}");
            if (enabled) Show(_lastTargetHwnd);
            else Hide();
        }
        else if (_overlayWindow != null && (key == "BackgroundMode" || key == "BackgroundColor" || key == "CurrentImageFileName"))
        {
            _loggingService.AddLog($"[OverlayService] Config '{key}' changed. Refreshing active overlay...");
            Show(_lastTargetHwnd);
        }
    }

    public void Show(IntPtr targetHwnd)
    {
        _lastTargetHwnd = targetHwnd;
        // Capture all configuration values on the calling thread (thread-safe reads).
        var backgroundMode = _configService.GetBackgroundMode();
        string? imagePath = null;
        string backgroundColor = "#FF000000"; // 默认黑色

        if (backgroundMode == BackgroundMode.IMAGE)
        {
            string? imageName = _configService.GetBackgroundImageFileName();
            if (!string.IsNullOrEmpty(imageName))
            {
                string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
                string fullPath = Path.Combine(backgroundsDir, imageName);
                if (File.Exists(fullPath))
                {
                    imagePath = fullPath;
                }
            }
        }
        else
        {
            backgroundColor = _configService.GetBackgroundColor();
        }

        // Marshal all Win32 window operations to the UI thread.
        UiDispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Dispose();
                _overlayWindow = null;
            }

            _overlayWindow = new OverlayWindowShell(imagePath, backgroundColor, _loggingService);

            IntPtr hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };

            int x = 0, y = 0, width = 800, height = 600;

            if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var monitorRect = monitorInfo.rcMonitor;
                x = monitorRect.Left;
                y = monitorRect.Top;
                width = monitorRect.Right - monitorRect.Left;
                height = monitorRect.Bottom - monitorRect.Top;
            }
            else
            {
                x = 0; y = 0; width = 1920; height = 1080;
            }

            _overlayWindow.Create(x, y, width, height);
            _overlayWindow.Show();

            // Synchronize Z-order
            var targetExStyle = (WindowExStyles)NativeMethods.GetWindowLong(targetHwnd, NativeMethods.GWL_EXSTYLE);
            bool isTargetTopmost = targetExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST);

            if (isTargetTopmost)
            {
                NativeMethods.SetWindowPos(_overlayWindow.Hwnd, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
            else
            {
                NativeMethods.SetWindowPos(_overlayWindow.Hwnd, new IntPtr(-2) /* HWND_NOTOPMOST */, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }

            NativeMethods.SetWindowPos(_overlayWindow.Hwnd, targetHwnd, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

            _loggingService.AddLog("[OverlayService] Native Overlay shown and synchronized.");
        });
    }

    public void Hide()
    {
        _lastTargetHwnd = IntPtr.Zero;
        UiDispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Dispose();
                _overlayWindow = null;
            }
            _loggingService.AddLog("[OverlayService] Native Overlay hidden.");
        });
    }

    public void UpdatePosition(IntPtr targetHwnd)
    {
        if (_overlayWindow == null) return;

        // Simply call Show again. Show() handles closing the old window and creating a new one
        // based on the LATEST monitor info and configuration.
        _loggingService.AddLog("[OverlayService] Orientation change detected. Re-syncing overlay window...");
        Show(targetHwnd);
    }

    public void Dispose()
    {
        _configService.ConfigChanged -= OnConfigChanged;
        Hide();
    }
}