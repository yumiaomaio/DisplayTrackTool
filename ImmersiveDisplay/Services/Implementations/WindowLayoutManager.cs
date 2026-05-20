// File: Services/Implementations/WindowLayoutManager.cs

using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

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

        // 1. Apply styles with failure detection
        int result = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)profile.Styles);
        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set window style GWL_STYLE. System Error Code: {error}");
            }
        }

        result = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)profile.ExStyles);
        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set window ex-style GWL_EXSTYLE. System Error Code: {error}");
            }
        }

        // 2. Calculate size and position
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

        var monitorRect = monitorInfo.rcMonitor;
        int finalWidth = monitorRect.Right - monitorRect.Left;
        int finalHeight = monitorRect.Bottom - monitorRect.Top;
        int finalX = monitorRect.Left;
        int finalY = monitorRect.Top;

        loggingService.AddLog($"[WindowLayoutManager] Layout: X={finalX}, Y={finalY}, W={finalWidth}, H={finalHeight}");

        // 3. Apply position
        IntPtr hwndInsertAfter = profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST) ? new IntPtr(-1) : IntPtr.Zero;
        bool posResult = NativeMethods.SetWindowPos(hwnd, hwndInsertAfter, finalX, finalY, finalWidth, finalHeight, 
            SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
        if (!posResult)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set window position. System Error Code: {error}");
            }
        }

        // 4. Order overlay window directly behind the target window
        var overlayHwnd = overlayService.WindowHandle;
        if (overlayHwnd.HasValue && overlayHwnd.Value != IntPtr.Zero)
        {
            // Sync topmost state
            bool isTargetTopmost = profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST);
            if (isTargetTopmost)
            {
                NativeMethods.SetWindowPos(overlayHwnd.Value, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, 
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
            else
            {
                NativeMethods.SetWindowPos(overlayHwnd.Value, new IntPtr(-2) /* HWND_NOTOPMOST */, 0, 0, 0, 0, 
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }

            // Chain behind target
            NativeMethods.SetWindowPos(overlayHwnd.Value, hwnd, 0, 0, 0, 0, 
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        }
    }

    public void ApplyAggressiveLayout(IntPtr hwnd, LayoutProfile profile)
    {
        if (hwnd == IntPtr.Zero) return;

        loggingService.AddLog($"[WindowLayoutManager] Applying AGGRESSIVE layout profile '{profile.Name}' to HWND {hwnd}.");

        // --- 1. Force Restore if Maximized ---
        if (NativeMethods.IsZoomed(hwnd) || NativeMethods.IsIconic(hwnd))
        {
            loggingService.AddLog("[WindowLayoutManager] Target is maximized or iconic. Forcing Restore (SW_RESTORE).");
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        // --- 2. Apply Styles with failure detection ---
        int result = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, (int)profile.Styles);
        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set aggressive window style GWL_STYLE. System Error Code: {error}");
            }
        }

        result = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)profile.ExStyles);
        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set aggressive window ex-style GWL_EXSTYLE. System Error Code: {error}");
            }
        }

        // --- 3. Get Monitor Info ---
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo)) return;

        var monitorRect = monitorInfo.rcMonitor;
        int finalWidth = monitorRect.Right - monitorRect.Left;
        int finalHeight = monitorRect.Bottom - monitorRect.Top;
        int finalX = monitorRect.Left;
        int finalY = monitorRect.Top;

        // --- 4. Step-by-step repositioning ---
        IntPtr hwndInsertAfter = profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST) ? new IntPtr(-1) : IntPtr.Zero;

        // --- 4.1 执行最终拉伸 ---
        var flags = SetWindowPosFlags.SWP_FRAMECHANGED | 
                    SetWindowPosFlags.SWP_NOACTIVATE | 
                    SetWindowPosFlags.SWP_SHOWWINDOW |
                    SetWindowPosFlags.SWP_NOSENDCHANGING;

        bool posResult = NativeMethods.SetWindowPos(hwnd, hwndInsertAfter, finalX, finalY, finalWidth, finalHeight, flags);
        if (!posResult)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set aggressive window position. System Error Code: {error}");
            }
        }

        // --- 4.2 Order overlay window directly behind the target window ---
        var overlayHwnd = overlayService.WindowHandle;
        if (overlayHwnd.HasValue && overlayHwnd.Value != IntPtr.Zero)
        {
            // Sync topmost state
            bool isTargetTopmost = profile.ExStyles.HasFlag(WindowExStyles.WS_EX_TOPMOST);
            if (isTargetTopmost)
            {
                NativeMethods.SetWindowPos(overlayHwnd.Value, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, 
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
            else
            {
                NativeMethods.SetWindowPos(overlayHwnd.Value, new IntPtr(-2) /* HWND_NOTOPMOST */, 0, 0, 0, 0, 
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }

            // Chain behind target
            NativeMethods.SetWindowPos(overlayHwnd.Value, hwnd, 0, 0, 0, 0, 
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        }
        
        // Final kick
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
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
        int result = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, (int)newExStyle);
        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set GWL_EXSTYLE for EnsureTopmost. System Error Code: {error}");
            }
        }
        
        var topmostHwnd = new IntPtr(-1);
        bool posResult = NativeMethods.SetWindowPos(hwnd, topmostHwnd, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOACTIVATE);
        if (!posResult)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error, $"Failed to set window position for EnsureTopmost. System Error Code: {error}");
            }
        }
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
