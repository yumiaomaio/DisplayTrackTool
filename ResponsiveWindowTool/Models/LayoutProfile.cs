// File: Models/LayoutProfile.cs
using ResponsiveWindowTool.Interop.Enums;

namespace ResponsiveWindowTool.Models;

public class LayoutProfile
{
    public string Name { get; set; } = "Unnamed Profile";
    public WindowStyles Styles { get; set; }
    public WindowExStyles ExStyles { get; set; }
    public SizingMode Sizing { get; set; }
    public PositioningMode Positioning { get; set; }
    public double? AspectRatio { get; set; } // e.g., 9.0 / 16.0 for portrait
}

public enum SizingMode
{
    FULLSCREEN,
    RELATIVE_TO_SCREEN_HEIGHT,
    // Potentially more modes later
}

public enum PositioningMode
{
    CENTER_SCREEN,
    TOP_LEFT
    // Potentially more modes later
}