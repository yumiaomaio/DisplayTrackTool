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
        private readonly IOverlayService _overlayService; // 新增依赖字段

        public WindowLayoutManager(IOverlayService overlayService) // 修改构造函数
        {
            _overlayService = overlayService;
        }

        public void ApplyLayout(IntPtr hwnd, LayoutProfile profile)
        {
            if (hwnd == IntPtr.Zero || profile == null) return;

            Debug.WriteLine($"[WindowLayoutManager] Applying profile '{profile.Name}' to HWND {hwnd}.");

            // 1. Apply styles
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)profile.Styles);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)profile.ExStyles);

            // 2. Calculate size and position (这部分代码不变)
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

            var monitorRect = monitorInfo.rcMonitor;
            int screenWidth = monitorRect.Right - monitorRect.Left;
            int screenHeight = monitorRect.Bottom - monitorRect.Top;

            int finalWidth, finalHeight, finalX, finalY;

            switch (profile.Sizing)
            {
                case SizingMode.Fullscreen:
                    finalWidth = screenWidth;
                    finalHeight = screenHeight;
                    break;
                case SizingMode.RelativeToScreenHeight:
                    finalHeight = screenHeight;
                    finalWidth = profile.AspectRatio.HasValue
                        ? (int)(finalHeight * profile.AspectRatio.Value)
                        : screenHeight / 2;
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

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
                default: throw new ArgumentOutOfRangeException();
            }

            Debug.WriteLine(
                $"[WindowLayoutManager] Calculated Layout: X={finalX}, Y={finalY}, W={finalWidth}, H={finalHeight}");

            // 3. Apply Z-Order, position and size
    
            // a. 将背景窗口置于非置顶窗口的最上层
            var overlayHwnd = _overlayService.WindowHandle;
            if (overlayHwnd.HasValue && overlayHwnd.Value != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(overlayHwnd.Value, (IntPtr)0 /*HWND_TOP*/, 0, 0, 0, 0, 
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
                Debug.WriteLine($"[WindowLayoutManager] Overlay HWND {overlayHwnd.Value} set to HWND_TOP.");
            }

            // b. 根据Profile决定目标窗口是否置顶
            IntPtr hwndInsertAfter = IntPtr.Zero; // 默认值 (HWND_TOP)
            if (profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                hwndInsertAfter = new IntPtr(-1); // HWND_TOPMOST
            }
    
            NativeMethods.SetWindowPos(hwnd, hwndInsertAfter, finalX, finalY, finalWidth, finalHeight, 
                SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
            Debug.WriteLine($"[WindowLayoutManager] Target HWND {hwnd} positioned. Topmost: {hwndInsertAfter == new IntPtr(-1)}");
        }
        
        public void EnsureTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // 1. 获取当前 ExStyle
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            // 2. 如果已经有 Topmost 标志，则什么都不做
            if (currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                return;
            }

            // 3. 添加 Topmost 标志并应用
            Debug.WriteLine($"[WindowLayoutManager] Patching HWND {hwnd} to add WS_EX_TOPMOST.");
            var newExStyle = currentExStyle | WindowExStyles.WS_EX_TOPMOST;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)newExStyle);
            
            // 4. 重新应用 Z-Order，但不改变位置和大小，以确保样式生效
            var topmostHwnd = new IntPtr(-1);
            NativeMethods.SetWindowPos(hwnd, topmostHwnd, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
        }
        
        public void RestoreToStandard(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
    
            Debug.WriteLine($"[WindowLayoutManager] Restoring HWND {hwnd} to a standard style.");

            // 定义一个安全的、标准的窗口样式
            var standardStyle = WindowStyles.WS_OVERLAPPEDWINDOW | WindowStyles.WS_VISIBLE;
            var standardExStyle = WindowExStyles.WS_EX_APPWINDOW;

            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)standardStyle);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)standardExStyle);

            // 获取原始窗口矩形，避免窗口变得过大或过小
            NativeMethods.GetWindowRect(hwnd, out var rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // 刷新窗口以应用样式
            NativeMethods.SetWindowPos(hwnd, 
                (IntPtr)(-2), // HWND_NOTOPMOST
                rect.Left, rect.Top, width, height,
                SetWindowPosFlags.SWP_FRAMECHANGED);
        }
        
    }
}