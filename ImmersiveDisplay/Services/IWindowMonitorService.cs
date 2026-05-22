using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services;

public interface IWindowMonitorService
{
    event Action<IntPtr>? WindowDestroyed;
    event Action<IntPtr, Rect>? WindowStateChanged;
    event Action<IntPtr, IntPtr>? MonitorChanged;

    void StartMonitoring(IntPtr targetHwnd);
    void StopMonitoring();
}
