// File: Models/AppConfig.cs

using System.Text.Json.Serialization;

namespace ImmersiveDisplay.Models;

public enum BackgroundMode
{
    SOLID_COLOR,
    IMAGE
}

public record AppConfig
{
    [JsonPropertyName("targetProcessName")]
    public string TargetProcessName { get; init; } = string.Empty;
    
    [JsonPropertyName("enableBackgroundOverlay")]
    public bool EnableBackgroundOverlay { get; init; }

    [JsonPropertyName("enableTaskbarAutoHide")]
    public bool EnableTaskbarAutoHide { get; init; }

    [JsonPropertyName("enableDisplaySync")]
    public bool EnableDisplaySync { get; init; }

    [JsonPropertyName("backgroundMode")]
    public BackgroundMode BackgroundMode { get; init; }

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; init; } = string.Empty;

    [JsonPropertyName("backgroundImageFileName")]
    public string? BackgroundImageFileName { get; init; }

    [JsonPropertyName("showExitTip")]
    public bool ShowExitTip { get; init; }

    [JsonPropertyName("associatedLaunchPath")]
    public string? AssociatedLaunchPath { get; init; }

    [JsonPropertyName("launchOnAppStartup")]
    public bool LaunchOnAppStartup { get; init; }

    [JsonPropertyName("launchOnTaskStart")]
    public bool LaunchOnTaskStart { get; init; }

    [JsonPropertyName("autoStartFromThirdParty")]
    public bool AutoStartFromThirdParty { get; init; }

    [JsonPropertyName("autoStartMonitoringOnProtocolLaunch")]
    public bool AutoStartMonitoringOnProtocolLaunch { get; init; }

    [JsonPropertyName("windowDetectionTimeout")]
    public int WindowDetectionTimeout { get; init; }

    [JsonPropertyName("enableFileLogging")]
    public bool EnableFileLogging { get; init; }

    [JsonPropertyName("profiles")]
    public ProfileCollection Profiles { get; init; } = new();

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
            AutoStartMonitoringOnProtocolLaunch = false,
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

public record ProfileCollection
{
    [JsonPropertyName("portrait")]
    public ProfileDefinition Portrait { get; init; } = new();

    [JsonPropertyName("landscape")]
    public ProfileDefinition Landscape { get; init; } = new();
}

public record DisplayProfile
{
    [JsonPropertyName("width")]
    public int? Width { get; init; }

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("orientation")]
    public int? Orientation { get; init; } // 0=Default, 1=90, 2=180, 3=270
}

public record ProfileDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "Unnamed";

    [JsonPropertyName("styles")]
    public List<string> Styles { get; init; } = new();
    
    [JsonPropertyName("exStyles")]
    public List<string> ExStyles { get; init; } = new();

    [JsonPropertyName("sizing")]
    public SizingMode Sizing { get; init; }

    [JsonPropertyName("positioning")]
    public PositioningMode Positioning { get; init; }

    [JsonPropertyName("display")]
    public DisplayProfile? Display { get; init; }
}