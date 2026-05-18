// File: Services/Implementations/KeyboardHookService.cs

using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services.Implementations;

public class KeyboardHookService : IKeyboardHookService, IDisposable
{
    public event Action<int>? KeyPressed;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // Keep a reference to prevent garbage collection
    private readonly ILoggingService _loggingService;

    public KeyboardHookService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        try
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }

            if (_hookId == IntPtr.Zero)
            {
                _loggingService.AddLog("[KeyboardHookService] Failed to set hook. Win32 Error: " + Marshal.GetLastWin32Error());
            }
            else
            {
                _loggingService.AddLog("[KeyboardHookService] Started.");
            }
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[KeyboardHookService] Exception during Start: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _loggingService.AddLog("[KeyboardHookService] Stopped.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            try
            {
                var kbdStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
                int vkCode = (int)kbdStruct.vkCode;
                KeyPressed?.Invoke(vkCode);
            }
            catch (Exception ex)
            {
                // Critical: Catching all exceptions here to prevent the hook from being detached by the OS
                _loggingService.AddLog($"[KeyboardHookService] Error in callback: {ex.Message}");
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }
}
