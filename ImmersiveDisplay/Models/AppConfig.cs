// File: Models/AppConfig.cs

using System.Text.Json.Serialization;

namespace ImmersiveDisplay.Models;

public enum BackgroundMode
{
    SOLID_COLOR,
    IMAGE
}

public class AppConfig
{
    [JsonPropertyName("targetProcessName")]
    public string TargetProcessName { get; set; } = string.Empty;
    
    [JsonPropertyName("enableBackgroundOverlay")]
    public bool EnableBackgroundOverlay { get; set; }

    [JsonPropertyName("enableTaskbarAutoHide")]
    public bool EnableTaskbarAutoHide { get; set; }

    [JsonPropertyName("enableDisplaySync")]
    public bool EnableDisplaySync { get; set; }

    [JsonPropertyName("backgroundMode")]
    public BackgroundMode BackgroundMode { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = string.Empty;

    [JsonPropertyName("backgroundImageFileName")]
    public string? BackgroundImageFileName { get; set; }

    [JsonPropertyName("showExitTip")]
    public bool ShowExitTip { get; set; }

    [JsonPropertyName("associatedLaunchPath")]
    public string? AssociatedLaunchPath { get; set; }

    [JsonPropertyName("launchOnAppStartup")]
    public bool LaunchOnAppStartup { get; set; }

    [JsonPropertyName("launchOnTaskStart")]
    public bool LaunchOnTaskStart { get; set; }

    [JsonPropertyName("autoStartFromThirdParty")]
    public bool AutoStartFromThirdParty { get; set; }

    [JsonPropertyName("windowDetectionTimeout")]
    public int WindowDetectionTimeout { get; set; }

    [JsonPropertyName("enableFileLogging")]
    public bool EnableFileLogging { get; set; }

    [JsonPropertyName("profiles")]
    public ProfileCollection Profiles { get; set; } = new();

    /// <summary>
    /// Static factory to create a default configuration.
    /// Acts as the single source of truth for all initial values.
    /// </summary>
    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            TargetProcessName = "notepad",
            EnableBackgroundOverlay = true,
            EnableTaskbarAutoHide = true,
            EnableDisplaySync = true,
            BackgroundMode = BackgroundMode.SOLID_COLOR,
            BackgroundColor = "#FF000000",
            ShowExitTip = true,
            LaunchOnAppStartup = false,
            LaunchOnTaskStart = false,
            AutoStartFromThirdParty = false,
            WindowDetectionTimeout = 10,
            EnableFileLogging = false, // Default is OFF as requested
            Profiles = new ProfileCollection
            {
                Portrait = new ProfileDefinition
                {
                    Name = "Portrait Mode",
                    Styles = ["WS_POPUP", "WS_VISIBLE"],
                    ExStyles = ["WS_EX_TOPMOST"],
                    Sizing = SizingMode.FULLSCREEN,
                    Positioning = PositioningMode.TOP_LEFT,
                    Display = new DisplayProfile { Orientation = 1 } // 90 Degrees
                },
                Landscape = new ProfileDefinition
                {
                    Name = "Landscape Fullscreen",
                    Styles = ["WS_POPUP", "WS_VISIBLE"],
                    ExStyles = ["WS_EX_TOPMOST"],
                    Sizing = SizingMode.FULLSCREEN,
                    Positioning = PositioningMode.TOP_LEFT,
                    Display = new DisplayProfile { Orientation = 0 } // Default
                }
            }
        };
    }
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