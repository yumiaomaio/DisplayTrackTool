// File: Interop/NativeMethods.Window.cs

using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Interop;

internal static partial class NativeMethods
{
    public const int GWLP_USERDATA = -21;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        return (int)GetWindowLongPtr(hWnd, nIndex).ToInt64();
    }

    public static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        return (int)SetWindowLongPtr(hWnd, nIndex, new IntPtr(dwNewLong)).ToInt64();
    }
    
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
    public static partial int GetWindowText(IntPtr hWnd, IntPtr lpString, int nMaxCount);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rect rcNormalPosition;
    }

    [LibraryImport("user32.dll")]
    public static partial int GetWindowDpiAwarenessContext(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AreDpiAwarenessContextsEqual(int dpiContextA, int dpiContextB);
    
    [LibraryImport("user32.dll")]
    public static partial int GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hWnd);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsZoomed(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(IntPtr hWinEventHook);

    public const int SW_RESTORE = 9;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_REORDER = 0x8004;
    public const uint EVENT_OBJECT_STATECHANGE = 0x800A;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
}