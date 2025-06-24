// File: Models/AppConfig.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ResponsiveWindowTool.Models
{
    public enum BackgroundMode
    {
        SolidColor,
        Image
    }

    public class AppConfig
    {
        [JsonPropertyName("targetProcessName")]
        public string TargetProcessName { get; set; } = "notepad";
        [JsonPropertyName("enableDisplaySettingsOverride")]
        public bool EnableDisplaySettingsOverride { get; set; } = true; 
        [JsonPropertyName("enableBackgroundOverlay")]
        public bool EnableBackgroundOverlay { get; set; } = true;

        [JsonPropertyName("backgroundMode")]
        public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.SolidColor;

        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#FF000000";

        [JsonPropertyName("backgroundImageFileName")]
        public string? BackgroundImageFileName { get; set; }

        [JsonPropertyName("profiles")]
        public ProfileCollection Profiles { get; set; } = new();
        
        [JsonPropertyName("displaySettings")]
        public DisplayConfigSettings DisplaySettings { get; set; } = new();

        [JsonPropertyName("requireConfirmationOnExit")]
        public bool RequireConfirmationOnExit { get; set; } = true;
        
    }
    
    public class DisplayConfigSettings
    {
        [JsonPropertyName("width")]
        public int Width { get; set; } = 1920;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 1080;

        [JsonPropertyName("dpi")]
        public int Dpi { get; set; } = 100;
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
}