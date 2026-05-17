// File: Services/IOverlayService.cs
namespace ImmersiveDisplay.Services;

public interface IOverlayService
{
    IntPtr? WindowHandle { get; }
    void Show(IntPtr targetHwnd);
    void Hide();
}