// File: Models/AppConfig.cs
using System.Text.Json.Serialization;

namespace ImmersiveWindow.Models;

public enum BackgroundMode
{
    SOLID_COLOR,
    IMAGE
}

public class AppConfig
{
    [JsonPropertyName("targetProcessName")]
    public string TargetProcessName { get; set; } = "notepad";
    
    [JsonPropertyName("enableBackgroundOverlay")]
    public bool EnableBackgroundOverlay { get; set; } = true;

    [JsonPropertyName("enableTaskbarAutoHide")]
    public bool EnableTaskbarAutoHide { get; set; } = true;

    [JsonPropertyName("enableDisplaySync")]
    public bool EnableDisplaySync { get; set; } = true;

    [JsonPropertyName("backgroundMode")]
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.SOLID_COLOR;

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = "#FF000000";

    [JsonPropertyName("backgroundImageFileName")]
    public string? BackgroundImageFileName { get; set; }

    [JsonPropertyName("showExitTip")]
    public bool ShowExitTip { get; set; } = true;

    [JsonPropertyName("profiles")]
    public ProfileCollection Profiles { get; set; } = new();
}

public class ProfileCollection
{
    [JsonPropertyName("portrait")]
    public ProfileDefinition Portrait { get; set; } = new();

    [JsonPropertyName("landscape")]
    public ProfileDefinition Landscape { get; set; } = new();
}

public class DisplayProfile
{
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("orientation")]
    public int? Orientation { get; set; } // 0=Default, 1=90, 2=180, 3=270
}

public class ProfileDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Unnamed";

    [JsonPropertyName("styles")]
    public List<string> Styles { get; set; } = new();
    
    [JsonPropertyName("exStyles")]
    public List<string> ExStyles { get; set; } = new();

    [JsonPropertyName("sizing")]
    public SizingMode Sizing { get; set; }

    [JsonPropertyName("positioning")]
    public PositioningMode Positioning { get; set; }

    [JsonPropertyName("display")]
    public DisplayProfile? Display { get; set; }
}