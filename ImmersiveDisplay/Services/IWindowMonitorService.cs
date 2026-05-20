// File: Services/IWindowMonitorService.cs

using System;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Services;

public interface IWindowMonitorService
{
    event Action<IntPtr, Rect> WindowStateChanged;
    event Action<IntPtr, IntPtr> MonitorChanged;
    event Action<IntPtr> WindowDestroyed; 
    void StartMonitoring(IntPtr hwnd);
    void StopMonitoring();
}