// File: Models/LayoutProfile.cs

using ImmersiveDisplay.Interop.Enums;

namespace ImmersiveDisplay.Models;

public class LayoutProfile
{
    public string Name { get; set; } = "Unnamed Profile";
    public WindowStyles Styles { get; set; }
    public WindowExStyles ExStyles { get; set; }
    public SizingMode Sizing { get; set; }
    public PositioningMode Positioning { get; set; }
    public DisplayProfile? Display { get; set; }
}

public enum SizingMode
{
    FULLSCREEN,
    // Potentially more modes later
}

public enum PositioningMode
{
    CENTER_SCREEN,
    TOP_LEFT
    // Potentially more modes later
}
