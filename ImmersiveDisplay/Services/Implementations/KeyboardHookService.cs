using System.Diagnostics;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services.Implementations;

public class KeyboardHookService : IKeyboardHookService
{
    private readonly WindowThread _windowThread;
    private readonly ILoggingService _loggingService;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

    public event Action<int>? KeyPressed;

    public KeyboardHookService(WindowThread windowThread, ILoggingService loggingService)
    {
        _windowThread = windowThread;
        _loggingService = loggingService;
        _hookProc = HookCallback;
    }

    public void Install()
    {
        _windowThread.Post(InstallInternal);
    }

    public void Uninstall()
    {
        _windowThread.Post(UninstallInternal);
    }

    // --- Internal (runs on WindowThread) ---

    private void InstallInternal()
    {
        if (_hookId != IntPtr.Zero) return;

        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _hookProc,
                NativeMethods.GetModuleHandle(curModule.ModuleName),
                0);

            if (_hookId == IntPtr.Zero)
                _loggingService.AddLog("[KeyboardHook] Failed. Error: " + Marshal.GetLastWin32Error());
            else
                _loggingService.AddLog("[KeyboardHook] Installed.");
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[KeyboardHook] Install exception: {ex.Message}");
        }
    }

    private void UninstallInternal()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            try
            {
                var kbdStruct = Marshal.PtrToStructure<Kbdllhookstruct>(lParam);
                KeyPressed?.Invoke((int)kbdStruct.vkCode);
            }
            catch (Exception ex)
            {
                _loggingService.AddLog($"[KeyboardHook] Callback error: {ex.Message}");
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
