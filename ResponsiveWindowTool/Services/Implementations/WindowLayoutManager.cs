// File: Services/Implementations/WindowLayoutManager.cs

using System.Diagnostics;
using System.Runtime.InteropServices;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services.Implementations;

public class WindowLayoutManager : IWindowLayoutManager
{
    private readonly IOverlayService _overlayService;

    public WindowLayoutManager(IOverlayService overlayService)
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

        // 2. Calculate size and position
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

        Debug.WriteLine($"[WindowLayoutManager] Patching HWND {hwnd} to add WS_EX_TOPMOST.");
        var newExStyle = currentExStyle | WindowExStyles.WS_EX_TOPMOST;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)newExStyle);
        
        var topmostHwnd = new IntPtr(-1);
        NativeMethods.SetWindowPos(hwnd, topmostHwnd, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
    }

    public WindowSnapshot TakeSnapshot(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) throw new ArgumentException("Invalid HWND", nameof(hwnd));

        var style = (WindowStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
        var exStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.GetWindowRect(hwnd, out var rect);

        return new WindowSnapshot
        {
            Style = style,
            ExStyle = exStyle,
            Rect = rect
        };
    }

    public void Restore(IntPtr hwnd, WindowSnapshot snapshot)
    {
        if (hwnd == IntPtr.Zero || snapshot == null) return;

        Debug.WriteLine($"[WindowLayoutManager] Restoring HWND {hwnd} to original styles and position.");

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)snapshot.Style);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)snapshot.ExStyle);

        int width = snapshot.Rect.Right - snapshot.Rect.Left;
        int height = snapshot.Rect.Bottom - snapshot.Rect.Top;

        IntPtr hwndInsertAfter = snapshot.ExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST) 
            ? new IntPtr(-1) 
            : new IntPtr(-2); // HWND_NOTOPMOST

        NativeMethods.SetWindowPos(hwnd, 
            hwndInsertAfter,
            snapshot.Rect.Left, snapshot.Rect.Top, width, height,
            SetWindowPosFlags.SWP_FRAMECHANGED);
    }
}