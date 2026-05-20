// File: Services/Implementations/OverlayService.cs

using System.Runtime.InteropServices;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Views;

namespace ImmersiveDisplay.Services.Implementations;

public class OverlayService(IConfigService configService, ILoggingService loggingService) : IOverlayService
{
    private OverlayWindowShell? _overlayWindow;
    public IntPtr? WindowHandle => _overlayWindow?.Hwnd;

    public void Show(IntPtr targetHwnd)
    {
        // Capture all configuration values on the calling thread (thread-safe reads).
        var backgroundMode = configService.GetBackgroundMode();
        string? imagePath = null;
        string backgroundColor = "#FF000000"; // 默认黑色

        if (backgroundMode == BackgroundMode.IMAGE)
        {
            string? imageName = configService.GetBackgroundImageFileName();
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
            backgroundColor = configService.GetBackgroundColor();
        }

        // Marshal all Win32 window operations to the UI thread.
        // CreateWindowEx / ShowWindow / DestroyWindow MUST execute on the thread
        // that owns the message pump, otherwise the overlay window has no message
        // processing and will hang, turn white, and eventually crash.
        UiDispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Dispose();
                _overlayWindow = null;
            }

            _overlayWindow = new OverlayWindowShell(imagePath, backgroundColor, loggingService);

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
                x = 0;
                y = 0;
                width = 1920;
                height = 1080;
            }

            _overlayWindow.Create(x, y, width, height);
            _overlayWindow.Show();

            // Synchronize topmost state and Z-order with target window.
            // If the target window has the topmost style, place the overlay window topmost.
            // If the target window does not, place the overlay window non-topmost.
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

            // Order the overlay window directly behind the target window
            NativeMethods.SetWindowPos(_overlayWindow.Hwnd, targetHwnd, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

            loggingService.AddLog("[OverlayService] Native Overlay shown and ordered behind target.");
        });
    }

    public void Hide()
    {
        // DestroyWindow must also be called on the UI thread that created the window.
        UiDispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Dispose();
                _overlayWindow = null;
            }
            loggingService.AddLog("[OverlayService] Native Overlay hidden.");
        });
    }
}