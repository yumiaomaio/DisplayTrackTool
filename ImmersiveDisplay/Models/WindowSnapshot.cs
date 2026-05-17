using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Models;

public class WindowSnapshot
{
    public WindowStyles Style { get; set; }
    public WindowExStyles ExStyle { get; set; }
    public Rect Rect { get; set; }
}