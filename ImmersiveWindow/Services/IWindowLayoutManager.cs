// File: Services/IWindowLayoutManager.cs
using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services;

public interface IWindowLayoutManager
{
    void CaptureOriginalState(IntPtr hwnd);
    void ApplyLayout(IntPtr hwnd, LayoutProfile profile);
    void EnsureTopmost(IntPtr hwnd);
    void RestoreOriginalState(IntPtr hwnd);
}
