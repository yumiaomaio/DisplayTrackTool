// File: Services/IWindowLayoutManager.cs
using System;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    public interface IWindowLayoutManager
    {
        void ApplyLayout(IntPtr hwnd, LayoutProfile profile);
        void EnsureTopmost(IntPtr hwnd);
    }
}