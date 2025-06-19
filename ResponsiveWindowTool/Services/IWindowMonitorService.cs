// File: Services/IWindowMonitorService.cs
using System;
using System.Windows; // For Rect

namespace ResponsiveWindowTool.Services
{
    public interface IWindowMonitorService
    {
        event Action<IntPtr, Rect> WindowStateChanged;
        event Action<IntPtr> WindowDestroyed; 
        void StartMonitoring(IntPtr hwnd);
        void StopMonitoring();
    }
}