using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services;

public interface IDisplayService
{
    void CaptureOriginalState(IntPtr hwnd);
    void ApplyDisplayProfile(IntPtr hwnd, DisplayProfile? profile);
    void RestoreOriginalState(IntPtr hwnd);
    DisplayConfigRotation? GetCurrentDisplayRotation(IntPtr hwnd);
}
