// File: Services/IWindowQueryService.cs
namespace ImmersiveDisplay.Services;

public interface IWindowQueryService
{
    IntPtr? FindWindowByProcessName(string processName);
}