// File: Services/Implementations/OverlayService.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;
using ResponsiveWindowTool.Views;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class OverlayService : IOverlayService
    {
        private OverlayWindow? _overlayWindow;

        public void Show(IntPtr targetHwnd)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }

            _overlayWindow = new OverlayWindow();

            // Determine which monitor the target window is on.
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(targetHwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

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
            Debug.WriteLine("[OverlayService] Overlay shown.");
        }

        public void Hide()
        {
            _overlayWindow?.Close();
            _overlayWindow = null;
            Debug.WriteLine("[OverlayService] Overlay hidden.");
        }
    }
}