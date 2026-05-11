// File: Interop/NativeMethods.Monitor.cs
using System.Runtime.InteropServices;
using ImmersiveWindow.Interop.Enums;
using ImmersiveWindow.Interop.Structs;

namespace ImmersiveWindow.Interop;

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref Monitorinfo lpmi);
}