// File: Services/IOverlayService.cs
using System;

namespace ResponsiveWindowTool.Services
{
    public interface IOverlayService
    {
        void Show(IntPtr targetHwnd);
        void Hide();
    }
}