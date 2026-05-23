using ImmersiveDisplay.Engine;

namespace ImmersiveDisplay;

internal class Program
{
    [STAThread]
    static void Main()
    {
        using var bridge = new HostBridge();
        bridge.Run();
    }
}
