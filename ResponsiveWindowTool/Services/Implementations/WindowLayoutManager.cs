// File: Services/Implementations/WindowLayoutManager.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class WindowLayoutManager : IWindowLayoutManager
    {
        public void ApplyLayout(IntPtr hwnd, LayoutProfile profile)
        {
            if (hwnd == IntPtr.Zero || profile == null) return;
            
            Debug.WriteLine($"[WindowLayoutManager] Applying profile '{profile.Name}' to HWND {hwnd}.");

            // 1. Apply styles
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)profile.Styles);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)profile.ExStyles);

            // 2. Calculate size and position
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;
            
            var monitorRect = monitorInfo.rcMonitor;
            int screenWidth = monitorRect.Right - monitorRect.Left;
            int screenHeight = monitorRect.Bottom - monitorRect.Top;

            int finalWidth, finalHeight, finalX, finalY;

            // Calculate size based on sizing mode
            switch (profile.Sizing)
            {
                case SizingMode.Fullscreen:
                    finalWidth = screenWidth;
                    finalHeight = screenHeight;
                    break;
                case SizingMode.RelativeToScreenHeight:
                    if (profile.AspectRatio.HasValue)
                    {
                        finalHeight = screenHeight;
                        finalWidth = (int)(finalHeight * profile.AspectRatio.Value);
                    }
                    else // Fallback if no aspect ratio
                    {
                        finalHeight = screenHeight;
                        finalWidth = screenHeight / 2;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Calculate position based on positioning mode
            switch (profile.Positioning)
            {
                case PositioningMode.CenterScreen:
                    finalX = monitorRect.Left + (screenWidth - finalWidth) / 2;
                    finalY = monitorRect.Top + (screenHeight - finalHeight) / 2;
                    break;
                case PositioningMode.TopLeft:
                    finalX = monitorRect.Left;
                    finalY = monitorRect.Top;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            Debug.WriteLine($"[WindowLayoutManager] Calculated Layout: X={finalX}, Y={finalY}, W={finalWidth}, H={finalHeight}");

            // 3. Apply position and size
            // Use SWP_FRAMECHANGED to force the window to redraw with new styles
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, finalX, finalY, finalWidth, finalHeight, SetWindowPosFlags.SWP_FRAMECHANGED);
        }
    }
}