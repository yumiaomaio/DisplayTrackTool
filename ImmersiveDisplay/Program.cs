using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Interop;

namespace ImmersiveDisplay;

internal class Program
{
    [STAThread]
    static void Main()
    {
        const string mutexName = "Local\\ImmersiveDisplay_SingleInstance";
        using var mutex = new Mutex(false, mutexName);

        bool firstInstance;
        try
        {
            firstInstance = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance terminated without releasing the mutex
            firstInstance = true;
        }

        if (!firstInstance)
        {
            // Another instance is already running — bring its window to front
            var hwnd = NativeMethods.FindWindow("ImmersiveHostWindow", null);
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            return;
        }

        var args = Environment.GetCommandLineArgs();
        bool isProtocolAutoStart = args.Length > 1 &&
            args[1].StartsWith("immersivedisplay://autostart", StringComparison.OrdinalIgnoreCase);

        using var bridge = new HostBridge(isProtocolAutoStart);
        bridge.Run();
    }
}
