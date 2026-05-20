// File: Interop/NativeMethods.Taskbar.cs

using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Interop;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Appbardata
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public Rect rc;
        public IntPtr lParam;
    }

    [LibraryImport("shell32.dll")]
    public static partial uint SHAppBarMessage(uint dwMessage, ref Appbardata pData);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindWindow(string lpClassName, string? lpWindowName);

    public const uint ABM_SETSTATE = 0x0000000a;
    public const uint ABM_GETSTATE = 0x00000004;
    public const int ABS_AUTOHIDE = 0x01;
    public const int ABS_ALWAYSONTOP = 0x02;
}