using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services;

public interface IDisplayService
{
    void CaptureOriginalState(IntPtr hwnd);
    void ApplyDisplayProfile(IntPtr hwnd, DisplayProfile? profile);
    void RestoreOriginalState(IntPtr hwnd);
}
