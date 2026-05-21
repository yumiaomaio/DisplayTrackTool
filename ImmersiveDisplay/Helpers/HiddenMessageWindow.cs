using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;

namespace ImmersiveDisplay.Helpers;

internal class HiddenMessageWindow : IDisposable
{
    private const string ClassName = "ImmersiveDisplayMessageWindow";
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private IntPtr _hwnd = IntPtr.Zero;
    private static bool _classRegistered = false;

    public IntPtr Hwnd => _hwnd;

    public HiddenMessageWindow()
    {
        _wndProcDelegate = WndProc;
        EnsureClassRegistered();
        CreateMessageWindow();
    }

    private void EnsureClassRegistered()
    {
        if (_classRegistered) return;

        var wndClass = new NativeMethods.WNDCLASSEX();
        wndClass.cbSize = (uint)Marshal.SizeOf(wndClass);
        wndClass.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        wndClass.hInstance = NativeMethods.GetModuleHandle(null!);
        
        IntPtr classNamePtr = Marshal.StringToHGlobalUni(ClassName);
        try
        {
            wndClass.lpszClassName = classNamePtr;
            if (NativeMethods.RegisterClassEx(in wndClass) == 0)
            {
                int error = Marshal.GetLastWin32Error();
                // If it's already registered, we can ignore
                if (error != 1410) // ERROR_CLASS_ALREADY_EXISTS
                {
                    throw new System.ComponentModel.Win32Exception(error, "Failed to register message window class.");
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }

        _classRegistered = true;
    }

    private void CreateMessageWindow()
    {
        // HWND_MESSAGE = -3. Only receives messages, not visible, not in enumeration.
        IntPtr HWND_MESSAGE = new IntPtr(-3);
        
        _hwnd = NativeMethods.CreateWindowEx(
            0,
            ClassName,
            "ImmersiveMessagePump",
            0, 0, 0, 0, 0,
            HWND_MESSAGE,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null!),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to create message-only window.");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == UiDispatcher.WM_DISPATCH)
        {
            UiDispatcher.InvokePending();
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
