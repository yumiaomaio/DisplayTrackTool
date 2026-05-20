// File: Interop/NativeMethods.Monitor.cs

using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Interop;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref Monitorinfo lpmi);
}