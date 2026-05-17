// File: Services/Implementations/WindowLayoutManager.cs

using System.Runtime.InteropServices;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Enums;
using ImmersiveWindow.Interop.Structs;
using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services.Implementations;

public class WindowLayoutManager(IOverlayService overlayService, ILoggingService loggingService)
    : IWindowLayoutManager
{
    private WindowSnapshot? _originalSnapshot;

    public void CaptureOriginalState(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        
        var style = (WindowStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        var exStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.GetWindowRect(hwnd, out var rect);

        _originalSnapshot = new WindowSnapshot
        {
            Style = style,
            ExStyle = exStyle,
            Rect = rect
        };
        
        loggingService.AddLog($"[WindowLayoutManager] Original state captured for HWND {hwnd}.");
    }

    public void ApplyLayout(IntPtr hwnd, LayoutProfile profile)
    {
        if (hwnd == IntPtr.Zero) return;

        loggingService.AddLog($"[WindowLayoutManager] Applying profile '{profile.Name}' to HWND {hwnd}.");

        // 1. Apply styles
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)profile.Styles);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)profile.ExStyles);

        // 2. Calculate size and position (Always Fullscreen in current logic)
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

        var monitorRect = monitorInfo.rcMonitor;
        int screenWidth = monitorRect.Right - monitorRect.Left;
        int screenHeight = monitorRect.Bottom - monitorRect.Top;

        int finalWidth = screenWidth;
        int finalHeight = screenHeight;
        int finalX = monitorRect.Left;
        int finalY = monitorRect.Top;

        loggingService.AddLog(
            $"[WindowLayoutManager] Layout: X={finalX}, Y={finalY}, W={finalWidth}, H={finalHeight}");

        // 3. Apply Z-Order, position and size

        // a. 将背景窗口置于非置顶窗口的最上层
        var overlayHwnd = overlayService.WindowHandle;
        if (overlayHwnd.HasValue && overlayHwnd.Value != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(overlayHwnd.Value, 0 /*HWND_TOP*/, 0, 0, 0, 0, 
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        }

        // b. 根据Profile决定目标窗口是否置顶
        IntPtr hwndInsertAfter = IntPtr.Zero; // 默认值 (HWND_TOP)
        if (profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST))
        {
            hwndInsertAfter = new IntPtr(-1); // HWND_TOPMOST
        }

        NativeMethods.SetWindowPos(hwnd, hwndInsertAfter, finalX, finalY, finalWidth, finalHeight, 
            SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
    }
    
    public void EnsureTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

        if (currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
        {
            return;
        }

        loggingService.AddLog($"[WindowLayoutManager] Patching HWND {hwnd} to add WS_EX_TOPMOST.");
        var newExStyle = currentExStyle | WindowExStyles.WS_EX_TOPMOST;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)newExStyle);
        
        var topmostHwnd = new IntPtr(-1);
        NativeMethods.SetWindowPos(hwnd, topmostHwnd, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
    }

    public void RestoreOriginalState(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _originalSnapshot == null) return;

        loggingService.AddLog($"[WindowLayoutManager] Restoring HWND {hwnd} to original styles and position.");

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)_originalSnapshot.Style);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)_originalSnapshot.ExStyle);

        int width = _originalSnapshot.Rect.Right - _originalSnapshot.Rect.Left;
        int height = _originalSnapshot.Rect.Bottom - _originalSnapshot.Rect.Top;

        IntPtr hwndInsertAfter = _originalSnapshot.ExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST) 
            ? new IntPtr(-1) 
            : new IntPtr(-2); // HWND_NOTOPMOST

        NativeMethods.SetWindowPos(hwnd, 
            hwndInsertAfter,
            _originalSnapshot.Rect.Left, _originalSnapshot.Rect.Top, width, height,
            SetWindowPosFlags.SWP_FRAMECHANGED);
            
        _originalSnapshot = null;
    }
}
