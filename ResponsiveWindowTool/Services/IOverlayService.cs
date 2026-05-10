// File: Services/IOverlayService.cs
namespace ResponsiveWindowTool.Services;

public interface IOverlayService
{
    IntPtr? WindowHandle { get; }
    void Show(IntPtr targetHwnd);
    void Hide();
}