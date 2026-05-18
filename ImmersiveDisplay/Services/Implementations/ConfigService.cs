// File: Services/Implementations/ConfigService.cs

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

public class ConfigService : IConfigService
{
    private const string ConfigFileName = "profiles.json";
    private readonly AppConfig _config;
    private readonly ILoggingService _loggingService;

    public ConfigService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
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
            _loggingService.AddLog($"[ConfigService] Config saved to '{configPath}'.");
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[ConfigService] Error saving config: {ex.Message}");
        }
    }
    
    private AppConfig LoadOrCreateConfig()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            _loggingService.AddLog($"[ConfigService] Config file not found. Creating default at '{configPath}'.");
            var defaultConfig = CreateDefaultConfig();
            var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
            File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, options));
            return defaultConfig;
        }

        try
        {
            _loggingService.AddLog($"[ConfigService] Loading config from '{configPath}'.");
            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } }) 
                   ?? CreateDefaultConfig();

            return config;
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[ConfigService] Error loading config: {ex.Message}. Using default.");
            return CreateDefaultConfig();
        }
    }

    private AppConfig CreateDefaultConfig()
    {
        return AppConfig.CreateDefault();
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
            Display = def.Display
        };
    }
    
    public BackgroundMode GetBackgroundMode() => _config.BackgroundMode;
    public string GetBackgroundColor() => _config.BackgroundColor;

    public void SetBackgroundMode(BackgroundMode mode)
    {
        if (_config.BackgroundMode == mode) return;
        _config.BackgroundMode = mode;
        SaveConfig();
    }

    public void SetBackgroundColor(string color)
    {
        if (string.Equals(_config.BackgroundColor, color, StringComparison.InvariantCultureIgnoreCase)) return;
        _config.BackgroundColor = color;
        SaveConfig();
    }

    public bool IsBackgroundOverlayEnabled() => _config.EnableBackgroundOverlay;

    public void SetEnableBackgroundOverlay(bool enabled)
    {
        if (_config.EnableBackgroundOverlay == enabled) return;
        _config.EnableBackgroundOverlay = enabled;
        SaveConfig();
    }

    public bool IsTaskbarAutoHideEnabled() => _config.EnableTaskbarAutoHide;

    public void SetEnableTaskbarAutoHide(bool enabled)
    {
        if (_config.EnableTaskbarAutoHide == enabled) return;
        _config.EnableTaskbarAutoHide = enabled;
        SaveConfig();
    }

    public bool IsDisplaySyncEnabled() => _config.EnableDisplaySync;

    public void SetEnableDisplaySync(bool enabled)
    {
        if (_config.EnableDisplaySync == enabled) return;
        _config.EnableDisplaySync = enabled;
        SaveConfig();
    }

    public bool ShouldShowExitTip() => _config.ShowExitTip;

    public void SetShowExitTip(bool show)
    {
        if (_config.ShowExitTip == show) return;
        _config.ShowExitTip = show;
        SaveConfig();
    }

    public string? GetAssociatedLaunchPath() => _config.AssociatedLaunchPath;
    public void SetAssociatedLaunchPath(string? path)
    {
        if (_config.AssociatedLaunchPath == path) return;
        _config.AssociatedLaunchPath = path;
        SaveConfig();
    }

    public bool IsLaunchOnAppStartupEnabled() => _config.LaunchOnAppStartup;
    public void SetLaunchOnAppStartup(bool enabled)
    {
        if (_config.LaunchOnAppStartup == enabled) return;
        _config.LaunchOnAppStartup = enabled;
        SaveConfig();
    }

    public bool IsLaunchOnTaskStartEnabled() => _config.LaunchOnTaskStart;
    public void SetLaunchOnTaskStart(bool enabled)
    {
        if (_config.LaunchOnTaskStart == enabled) return;
        _config.LaunchOnTaskStart = enabled;
        SaveConfig();
    }

    public bool IsAutoStartFromThirdPartyEnabled() => _config.AutoStartFromThirdParty;
    public void SetAutoStartFromThirdParty(bool enabled)
    {
        if (_config.AutoStartFromThirdParty == enabled) return;
        _config.AutoStartFromThirdParty = enabled;
        SaveConfig();
    }

    private T ParseEnum<T>(List<string> values) where T : struct
    {
        if (!values.Any()) return default;

        uint rawValue = values
            .Select(s => Convert.ToUInt32(Enum.Parse(typeof(T), s, true)))
            .Aggregate(0U, (current, next) => current | next);

        return (T)(object)rawValue;
    }
}
