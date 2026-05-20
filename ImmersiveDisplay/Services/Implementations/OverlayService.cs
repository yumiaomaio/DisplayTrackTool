// File: Services/Implementations/OverlayService.cs

using System;
using System.IO;
using System.Runtime.InteropServices;
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
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose();
            _overlayWindow = null;
        }

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

        _overlayWindow = new OverlayWindowShell(imagePath, backgroundColor);

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
        loggingService.AddLog("[OverlayService] Native Overlay shown.");
    }

    public void Hide()
    {
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose();
            _overlayWindow = null;
        }
        loggingService.AddLog("[OverlayService] Native Overlay hidden.");
    }
}