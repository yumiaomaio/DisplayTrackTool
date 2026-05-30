using System.Runtime.InteropServices;
using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Views;

namespace ImmersiveDisplay.Services.Components;

public class OverlayService
{
    private readonly WindowThread _windowThread;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private IntPtr _lastTargetHwnd = IntPtr.Zero;

    // Owned on the WindowThread — accessed from both threads
    private OverlayWindowShell? _overlayWindow;

    public OverlayService(WindowThread windowThread, IConfigService configService, ILoggingService loggingService)
    {
        _windowThread = windowThread;
        _configService = configService;
        _loggingService = loggingService;

        _configService.ConfigChanged += OnConfigChanged;
    }

    public void Show(IntPtr targetHwnd)
    {
        _lastTargetHwnd = targetHwnd;
        _windowThread.Post(() => ShowInternal(targetHwnd));
    }

    public void Hide()
    {
        _lastTargetHwnd = IntPtr.Zero;
        _windowThread.Post(HideInternal);
    }

    public void UpdatePosition(IntPtr targetHwnd)
    {
        _windowThread.Send(() =>
        {
            if (_overlayWindow == null || _overlayWindow.Hwnd == IntPtr.Zero) return;
            _loggingService.AddLog("[OverlayService] Re-syncing overlay position...");

            var hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var mr = monitorInfo.rcMonitor;
                _overlayWindow.Reposition(mr.Left, mr.Top, mr.Right - mr.Left, mr.Bottom - mr.Top);
            }

            // Re-affirm non-topmost layer — never use a window handle as hWndInsertAfter
            NativeMethods.SetWindowPos(_overlayWindow.Hwnd, new IntPtr(-2), 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        });
    }

    // --- Internal (runs on WindowThread) ---

    private void ShowInternal(IntPtr targetHwnd)
    {
        HideInternal();

        var backgroundMode = _configService.GetBackgroundMode();
        string? imagePath = null;
        string backgroundColor = "#FF000000";

        if (backgroundMode == BackgroundMode.IMAGE)
        {
            string? imageName = _configService.GetBackgroundImageFileName();
            if (!string.IsNullOrEmpty(imageName))
            {
                string fullPath = Path.Combine(AppContext.BaseDirectory, "Backgrounds", imageName);
                if (File.Exists(fullPath)) imagePath = fullPath;
            }
        }
        else
        {
            backgroundColor = _configService.GetBackgroundColor();
        }

        _overlayWindow = new OverlayWindowShell(imagePath, backgroundColor, _loggingService);

        var hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };

        int x = 0, y = 0, width = 800, height = 600;
        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var mr = monitorInfo.rcMonitor;
            x = mr.Left; y = mr.Top;
            width = mr.Right - mr.Left;
            height = mr.Bottom - mr.Top;
        }

        _overlayWindow.Create(x, y, width, height);
        _overlayWindow.Show();

        // Explicitly place overlay in non-topmost layer (HWND_NOTOPMOST)
        // Never use a window handle as hWndInsertAfter — it can infect the overlay with topmost
        NativeMethods.SetWindowPos(_overlayWindow.Hwnd, new IntPtr(-2), 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

        _loggingService.AddLog("[OverlayService] Overlay shown.");
    }

    private void HideInternal()
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose();
            _overlayWindow = null;
            _loggingService.AddLog("[OverlayService] Overlay hidden.");
        }
    }

    private void OnConfigChanged(AppConfig config)
    {
        if (_lastTargetHwnd == IntPtr.Zero) return;
        _loggingService.AddLog("[OverlayService] Config changed. Refreshing overlay...");
        Show(_lastTargetHwnd);
    }
}
