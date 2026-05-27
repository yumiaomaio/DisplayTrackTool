using System.Text.Json.Serialization;

namespace ImmersiveDisplay.Models;

public enum BackgroundMode
{
    COLOR,
    IMAGE
}

public class AppConfig
{
    public string TargetProcessName { get; set; } = string.Empty;
    public bool EnableBackgroundOverlay { get; set; }
    public bool EnableTaskbarAutoHide { get; set; }
    public bool EnableDisplaySync { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<BackgroundMode>))]
    public BackgroundMode BackgroundMode { get; set; }

    public string BackgroundColor { get; set; } = string.Empty;
    public string? BackgroundImageFileName { get; set; }
    public bool ShowExitTip { get; set; }
    public string? AssociatedLaunchPath { get; set; }
    public bool LaunchOnAppStartup { get; set; }
    public bool LaunchOnTaskStart { get; set; }
    public bool AutoStartFromThirdParty { get; set; }
    public bool ProtocolRegistrationEnabled { get; set; }
    public bool AutoStartMonitoringOnProtocolLaunch { get; set; }
    public int WindowDetectionTimeout { get; set; }
    public bool EnableFileLogging { get; set; }
    public ProfileCollection Profiles { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            TargetProcessName = "notepad",
            EnableBackgroundOverlay = true,
            EnableTaskbarAutoHide = true,
            EnableDisplaySync = true,
            BackgroundMode = BackgroundMode.COLOR,
            BackgroundColor = "#FF8C00",
            ShowExitTip = true,
            LaunchOnAppStartup = false,
            LaunchOnTaskStart = false,
            AutoStartFromThirdParty = false,
            AutoStartMonitoringOnProtocolLaunch = false,
            WindowDetectionTimeout = 20,
            EnableFileLogging = false,
            Profiles = new ProfileCollection
            {
                Portrait = new ProfileDefinition
                {
                    Name = "Portrait Mode",
                    Styles = ["WS_POPUP", "WS_VISIBLE"],
                    ExStyles = ["WS_EX_TOPMOST"],
                    Sizing = SizingMode.FULLSCREEN,
                    Positioning = PositioningMode.TOP_LEFT,
                    Display = new DisplayProfile { Orientation = 1 }
                },
                Landscape = new ProfileDefinition
                {
                    Name = "Landscape Fullscreen",
                    Styles = ["WS_POPUP", "WS_VISIBLE"],
                    ExStyles = ["WS_EX_TOPMOST"],
                    Sizing = SizingMode.FULLSCREEN,
                    Positioning = PositioningMode.TOP_LEFT,
                    Display = new DisplayProfile { Orientation = 0 }
                }
            }
        };
    }
}

public class ProfileCollection
{
    public ProfileDefinition Portrait { get; set; } = new();
    public ProfileDefinition Landscape { get; set; } = new();
}

public class DisplayProfile
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Orientation { get; set; }
}

public class ProfileDefinition
{
    public string Name { get; set; } = "Unnamed";
    public List<string> Styles { get; set; } = new();
    public List<string> ExStyles { get; set; } = new();

    [JsonConverter(typeof(JsonStringEnumConverter<SizingMode>))]
    public SizingMode Sizing { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<PositioningMode>))]
    public PositioningMode Positioning { get; set; }

    public DisplayProfile? Display { get; set; }
}
