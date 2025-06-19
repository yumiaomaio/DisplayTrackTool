// File: Services/Implementations/ConfigService.cs (Final Corrected Version)

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "profiles.json";
        private readonly AppConfig _config;

        public ConfigService()
        {
            _config = LoadOrCreateConfig();
        }

        public string GetDefaultProcessName() => _config.TargetProcessName;
        public void SetDefaultProcessName(string processName)
        {
            if (_config.TargetProcessName == processName) return;
            _config.TargetProcessName = processName;
            SaveConfig();
        }
        public string? GetBackgroundImageFileName() => _config.BackgroundImageFileName;

        public LayoutProfile GetPortraitProfile() => ConvertToLayoutProfile(_config.Profiles.Portrait);
        public LayoutProfile GetLandscapeProfile() => ConvertToLayoutProfile(_config.Profiles.Landscape);

        public double GetPortraitAspectRatio() => _config.Profiles.Portrait.AspectRatio ?? (9.0 / 16.0); // 新增
        public void SetPortraitAspectRatio(double aspectRatio) // 新增
        {
            if (_config.Profiles.Portrait.AspectRatio == aspectRatio) return;
            _config.Profiles.Portrait.AspectRatio = aspectRatio;
            SaveConfig();
        }

        public void SetBackgroundImageFileName(string? fileName)
        {
            if (_config.BackgroundImageFileName == fileName) return;
            _config.BackgroundImageFileName = fileName;
            SaveConfig();
        }
        
        private void SaveConfig()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                File.WriteAllText(configPath, JsonSerializer.Serialize(_config, options));
                Debug.WriteLine($"[ConfigService] Config saved to '{configPath}'.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Error saving config: {ex.Message}");
            }
        }
        
        private AppConfig LoadOrCreateConfig()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            if (!File.Exists(configPath))
            {
                Debug.WriteLine($"[ConfigService] Config file not found. Creating default at '{configPath}'.");
                var defaultConfig = CreateDefaultConfig();
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, options));
                return defaultConfig;
            }

            try
            {
                Debug.WriteLine($"[ConfigService] Loading config from '{configPath}'.");
                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } }) 
                       ?? CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Error loading config: {ex.Message}. Using default.");
                return CreateDefaultConfig();
            }
        }

        private AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                TargetProcessName = "notepad",
                BackgroundImageFileName = null,
                Profiles = new ProfileCollection
                {
                    Portrait = new ProfileDefinition
                    {
                        Name = "Portrait Mode",
                        Styles = new List<string> { "WS_POPUP", "WS_VISIBLE" },
                        ExStyles = new List<string> { "WS_EX_TOPMOST" },
                        Sizing = SizingMode.RelativeToScreenHeight,
                        Positioning = PositioningMode.CenterScreen,
                        AspectRatio = 9.0 / 16.0
                    },
                    Landscape = new ProfileDefinition
                    {
                        Name = "Landscape Fullscreen",
                        Styles = new List<string> { "WS_POPUP", "WS_VISIBLE" },
                        ExStyles = new List<string> { "WS_EX_TOPMOST" },
                        Sizing = SizingMode.Fullscreen,
                        Positioning = PositioningMode.TopLeft
                    }
                }
            };
        }
        
        private LayoutProfile ConvertToLayoutProfile(ProfileDefinition def)
        {
            return new LayoutProfile
            {
                Name = def.Name,
                Styles = ParseEnum<WindowStyles>(def.Styles),
                ExStyles = ParseEnum<WindowExStyles>(def.ExStyles),
                Sizing = def.Sizing,
                Positioning = def.Positioning,
                AspectRatio = def.AspectRatio
            };
        }

        private T ParseEnum<T>(List<string> values) where T : struct
        {
            if (values == null || !values.Any()) return default;

            uint rawValue = values
                .Select(s => Convert.ToUInt32(Enum.Parse(typeof(T), s, true)))
                .Aggregate(0U, (current, next) => current | next);

            return (T)(object)rawValue;
        }
    }
}