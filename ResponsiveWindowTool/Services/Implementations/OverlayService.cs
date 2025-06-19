// File: Services/Implementations/OverlayService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;
using ResponsiveWindowTool.Views;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class OverlayService : IOverlayService
    {
        private OverlayWindow? _overlayWindow;
        private readonly IConfigService _configService;
        public IntPtr? WindowHandle { get; private set; } // 新增属性

        public OverlayService(IConfigService configService) // <-- 修改构造函数
        {
            _configService = configService;
        }
        
        public void Show(IntPtr targetHwnd)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }
            
            // 从配置服务获取图片文件名
            string? imageName = _configService.GetBackgroundImageFileName();
            string? imagePath = null;

            if (!string.IsNullOrEmpty(imageName))
            {
                // 构建完整路径
                string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
                imagePath = Path.Combine(backgroundsDir, imageName);
                if (!File.Exists(imagePath))
                {
                    Debug.WriteLine($"[OverlayService] Background image not found at: {imagePath}. Reverting to black.");
                    imagePath = null;
                }
            }

            _overlayWindow = new OverlayWindow(imagePath); // <-- 将路径传递给窗口

            // 句柄获取，确保 SourceInitialized 后可用
            _overlayWindow.SourceInitialized += (s, e) =>
            {
                WindowHandle = new WindowInteropHelper(_overlayWindow).Handle;
            };

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
            WindowHandle = null; // 清理句柄
            Debug.WriteLine("[OverlayService] Overlay hidden.");
        }
    }
}