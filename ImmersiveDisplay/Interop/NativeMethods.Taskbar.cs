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

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint SHAppBarMessage(uint dwMessage, ref Appbardata pData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    public const uint ABM_SETSTATE = 0x0000000a;
    public const uint ABM_GETSTATE = 0x00000004;
    public const int ABS_AUTOHIDE = 0x01;
    public const int ABS_ALWAYSONTOP = 0x02;
}