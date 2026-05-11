// File: Services/IWindowQueryService.cs
namespace ImmersiveWindow.Services;

public interface IWindowQueryService
{
    IntPtr? FindWindowByProcessName(string processName);
}