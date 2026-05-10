// File: Services/IWindowQueryService.cs
namespace ResponsiveWindowTool.Services;

public interface IWindowQueryService
{
    IntPtr? FindWindowByProcessName(string processName);
}