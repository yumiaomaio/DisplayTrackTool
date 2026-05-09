using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Interop.Structs;

namespace ResponsiveWindowTool.Models
{
    public class WindowSnapshot
    {
        public WindowStyles Style { get; set; }
        public WindowExStyles ExStyle { get; set; }
        public RECT Rect { get; set; }
    }
}