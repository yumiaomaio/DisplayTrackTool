// File: Services/IOverlayService.cs
namespace ImmersiveWindow.Services;

public interface IOverlayService
{
    IntPtr? WindowHandle { get; }
    void Show(IntPtr targetHwnd);
    void Hide();
}