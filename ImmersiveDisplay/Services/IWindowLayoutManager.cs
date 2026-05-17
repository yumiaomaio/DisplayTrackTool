// File: Services/IWindowLayoutManager.cs

using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services;

public interface IWindowLayoutManager
{
    void CaptureOriginalState(IntPtr hwnd);
    void ApplyLayout(IntPtr hwnd, LayoutProfile profile);
    void EnsureTopmost(IntPtr hwnd);
    void RestoreOriginalState(IntPtr hwnd);
}
