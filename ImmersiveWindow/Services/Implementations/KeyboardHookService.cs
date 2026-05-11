// File: Services/Implementations/KeyboardHookService.cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveWindow.Interop;
using ImmersiveWindow.Interop.Structs;

namespace ImmersiveWindow.Services.Implementations;

public class KeyboardHookService : IKeyboardHookService, IDisposable
{
    public event Action<int>? KeyPressed;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // Keep a reference to prevent garbage collection

    public KeyboardHookService()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule!)
        {
            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc,
                NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
        }
        Debug.WriteLine("[KeyboardHookService] Started.");
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        Debug.WriteLine("[KeyboardHookService] Stopped.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            var kbdStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
            int vkCode = (int)kbdStruct.vkCode;
            KeyPressed?.Invoke(vkCode);
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }
}