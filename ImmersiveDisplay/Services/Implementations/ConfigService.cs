// File: Services/Implementations/ConfigService.cs

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

public class ConfigService : IConfigService
{
    public event Action<string, object?>? ConfigChanged;
    private const string ConfigFileName = "profiles.json";
    private AppConfig _config;
    private readonly ILoggingService _loggingService;

    public ConfigService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _config = LoadOrCreateConfig();
        
        // Initialize file logging based on config
        _loggingService.EnableFileLogging(_config.EnableFileLogging);
    }

    public string GetDefaultProcessName() => _config.TargetProcessName;
    public void SetDefaultProcessName(string processName)
    {
        if (_config.TargetProcessName == processName) return;
        _config = _config with { TargetProcessName = processName };
        SaveConfig();
        ConfigChanged?.Invoke("TargetProcessName", processName);
    }
    public string? GetBackgroundImageFileName() => _config.BackgroundImageFileName;

    public LayoutProfile GetPortraitProfile() => ConvertToLayoutProfile(_config.Profiles.Portrait);
    public LayoutProfile GetLandscapeProfile() => ConvertToLayoutProfile(_config.Profiles.Landscape);

    public void SetBackgroundImageFileName(string? fileName)
    {
        if (_config.BackgroundImageFileName == fileName) return;
        _config = _config with { BackgroundImageFileName = fileName };
        SaveConfig();
        ConfigChanged?.Invoke("CurrentImageFileName", fileName);
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
        _config = _config with { BackgroundMode = mode };
        SaveConfig();
        ConfigChanged?.Invoke("BackgroundMode", mode.ToString().ToLower());
    }

    public void SetBackgroundColor(string color)
    {
        if (string.Equals(_config.BackgroundColor, color, StringComparison.InvariantCultureIgnoreCase)) return;
        _config = _config with { BackgroundColor = color };
        SaveConfig();
        ConfigChanged?.Invoke("BackgroundColor", color);
    }

    public bool IsBackgroundOverlayEnabled() => _config.EnableBackgroundOverlay;

    public void SetEnableBackgroundOverlay(bool enabled)
    {
        if (_config.EnableBackgroundOverlay == enabled) return;
        _config = _config with { EnableBackgroundOverlay = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("EnableBackgroundOverlay", enabled);
    }

    public bool IsTaskbarAutoHideEnabled() => _config.EnableTaskbarAutoHide;

    public void SetEnableTaskbarAutoHide(bool enabled)
    {
        if (_config.EnableTaskbarAutoHide == enabled) return;
        _config = _config with { EnableTaskbarAutoHide = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("EnableTaskbarAutoHide", enabled);
    }

    public bool IsDisplaySyncEnabled() => _config.EnableDisplaySync;

    public void SetEnableDisplaySync(bool enabled)
    {
        if (_config.EnableDisplaySync == enabled) return;
        _config = _config with { EnableDisplaySync = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("EnableDisplaySync", enabled);
    }

    public bool ShouldShowExitTip() => _config.ShowExitTip;

    public void SetShowExitTip(bool show)
    {
        if (_config.ShowExitTip == show) return;
        _config = _config with { ShowExitTip = show };
        SaveConfig();
        ConfigChanged?.Invoke("ShouldShowExitTip", show);
    }

    public string? GetAssociatedLaunchPath() => _config.AssociatedLaunchPath;
    public void SetAssociatedLaunchPath(string? path)
    {
        if (_config.AssociatedLaunchPath == path) return;
        _config = _config with { AssociatedLaunchPath = path };
        SaveConfig();
        ConfigChanged?.Invoke("AssociatedLaunchPath", path);
    }

    public bool IsLaunchOnAppStartupEnabled() => _config.LaunchOnAppStartup;
    public void SetLaunchOnAppStartup(bool enabled)
    {
        if (_config.LaunchOnAppStartup == enabled) return;
        _config = _config with { LaunchOnAppStartup = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("LaunchOnAppStartup", enabled);
    }

    public bool IsLaunchOnTaskStartEnabled() => _config.LaunchOnTaskStart;
    public void SetLaunchOnTaskStart(bool enabled)
    {
        if (_config.LaunchOnTaskStart == enabled) return;
        _config = _config with { LaunchOnTaskStart = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("LaunchOnTaskStart", enabled);
    }

    public bool IsAutoStartFromThirdPartyEnabled() => _config.AutoStartFromThirdParty;
    public void SetAutoStartFromThirdParty(bool enabled)
    {
        if (_config.AutoStartFromThirdParty == enabled) return;
        _config = _config with { AutoStartFromThirdParty = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("AutoStartFromThirdParty", enabled);
    }

    public bool IsAutoStartMonitoringOnProtocolLaunchEnabled() => _config.AutoStartMonitoringOnProtocolLaunch;
    public void SetAutoStartMonitoringOnProtocolLaunch(bool enabled)
    {
        if (_config.AutoStartMonitoringOnProtocolLaunch == enabled) return;
        _config = _config with { AutoStartMonitoringOnProtocolLaunch = enabled };
        SaveConfig();
        ConfigChanged?.Invoke("AutoStartMonitoringOnProtocolLaunch", enabled);
    }

    public int GetWindowDetectionTimeout() => _config.WindowDetectionTimeout;
    public void SetWindowDetectionTimeout(int seconds)
    {
        if (_config.WindowDetectionTimeout == seconds) return;
        _config = _config with { WindowDetectionTimeout = seconds };
        SaveConfig();
        ConfigChanged?.Invoke("WindowDetectionTimeout", seconds);
    }

    private T ParseEnum<T>(System.Collections.Generic.List<string> values) where T : struct
    {
        if (!values.Any()) return default;

        uint rawValue = values
            .Select(s => Convert.ToUInt32(Enum.Parse(typeof(T), s, true)))
            .Aggregate(0U, (current, next) => current | next);

        return (T)(object)rawValue;
    }
}
