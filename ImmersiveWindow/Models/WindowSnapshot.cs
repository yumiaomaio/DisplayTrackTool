using ImmersiveWindow.Interop.Enums;
using ImmersiveWindow.Interop.Structs;

namespace ImmersiveWindow.Models;

public class WindowSnapshot
{
    public WindowStyles Style { get; set; }
    public WindowExStyles ExStyle { get; set; }
    public Rect Rect { get; set; }
}