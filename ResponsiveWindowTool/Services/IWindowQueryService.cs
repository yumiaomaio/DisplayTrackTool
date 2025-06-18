// File: Services/IWindowQueryService.cs
using System;

namespace ResponsiveWindowTool.Services
{
    public interface IWindowQueryService
    {
        IntPtr? FindWindowByProcessName(string processName);
    }
}