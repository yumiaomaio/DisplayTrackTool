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

        [JsonPropertyName("backgroundMode")]
        public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.SolidColor; // <-- 默认使用纯色

        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#FF000000"; // <-- 默认纯黑 (ARGB)

        [JsonPropertyName("backgroundImageFileName")]
        public string? BackgroundImageFileName { get; set; }

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
}