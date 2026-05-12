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

    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; set; }
}