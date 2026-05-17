// File: Services/Implementations/OverlayService.cs

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Views;

namespace ImmersiveDisplay.Services.Implementations;

public class OverlayService(IConfigService configService, ILoggingService loggingService) : IOverlayService
{
    private OverlayWindow? _overlayWindow;
    public IntPtr? WindowHandle { get; private set; }

    public void Show(IntPtr targetHwnd)
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
        }

        var backgroundMode = configService.GetBackgroundMode();
        string? imagePath = null;
        string backgroundColor = "#FF000000"; // 默认黑色

        if (backgroundMode == BackgroundMode.IMAGE)
        {
            // 图片模式逻辑
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
            // 纯色模式逻辑
            backgroundColor = configService.GetBackgroundColor();
        }

        // 将两种可能的值都传递给 OverlayWindow
        _overlayWindow = new OverlayWindow(imagePath, backgroundColor);

        _overlayWindow.SourceInitialized += (_, _) =>
        {
            WindowHandle = new WindowInteropHelper(_overlayWindow).Handle;
        };

        IntPtr hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };

        if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var monitorRect = monitorInfo.rcMonitor;
            _overlayWindow.Left = monitorRect.Left;
            _overlayWindow.Top = monitorRect.Top;
            _overlayWindow.Width = monitorRect.Right - monitorRect.Left;
            _overlayWindow.Height = monitorRect.Bottom - monitorRect.Top;
            _overlayWindow.WindowState = WindowState.Normal; // Ensure it's not maximized in a weird way
        }
        else
        {
            // Fallback to primary screen
            _overlayWindow.WindowState = WindowState.Maximized;
        }

        _overlayWindow.Show();
        loggingService.AddLog("[OverlayService] Overlay shown.");
    }

    public void Hide()
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
        WindowHandle = null; // 清理句柄
        loggingService.AddLog("[OverlayService] Overlay hidden.");
    }
}