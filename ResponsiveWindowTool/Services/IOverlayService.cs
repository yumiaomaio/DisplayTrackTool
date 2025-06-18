// File: Services/IOverlayService.cs
using System;

namespace ResponsiveWindowTool.Services
{
    public interface IOverlayService
    {
        IntPtr? WindowHandle { get; }
        void Show(IntPtr targetHwnd);
        void Hide();
    }
}