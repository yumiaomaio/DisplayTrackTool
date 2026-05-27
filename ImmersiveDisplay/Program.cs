using ImmersiveDisplay.Engine;

namespace ImmersiveDisplay;

internal class Program
{
    [STAThread]
    static void Main()
    {
        var args = Environment.GetCommandLineArgs();
        bool isProtocolAutoStart = args.Length > 1 &&
            args[1].StartsWith("immersivedisplay://autostart", StringComparison.OrdinalIgnoreCase);

        using var bridge = new HostBridge(isProtocolAutoStart);
        bridge.Run();
    }
}
