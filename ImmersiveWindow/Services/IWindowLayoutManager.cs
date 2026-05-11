// File: Services/IWindowLayoutManager.cs
using ImmersiveWindow.Models;

namespace ImmersiveWindow.Services;

public interface IWindowLayoutManager
{
    void ApplyLayout(IntPtr hwnd, LayoutProfile profile);
    void EnsureTopmost(IntPtr hwnd);
    WindowSnapshot TakeSnapshot(IntPtr hwnd);
    void Restore(IntPtr hwnd, WindowSnapshot snapshot);
}