// File: Services/Implementations/ConfigService.cs

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Interop.Enums;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Services.Implementations;

public class ConfigService : IConfigService
{
    public event Action<AppConfig>? ConfigChanged;
    private const string ConfigFileName = "profiles.json";
    private readonly AppConfig _config;
    private readonly ILoggingService _loggingService;

    public ConfigService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _config = LoadOrCreateConfig();
        
        // Initialize file logging based on config
        _loggingService.EnableFileLogging(_config.EnableFileLogging);
    }

    private void Update(Action<AppConfig> action, [CallerMemberName] string caller = "")
    {
        var configName = caller.StartsWith("Set") ? caller[3..] : caller;
        action(_config);
        _loggingService.AddLog($"[ConfigService] Saving config: {configName}");
        SaveConfig();
        NotifyConfigChanged();
    }

    private void NotifyConfigChanged() => ConfigChanged?.Invoke(_config);

    private void SaveConfig()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        try
        {
            File.WriteAllText(configPath, JsonSerializer.Serialize(_config, AppJsonContext.Default.AppConfig));
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[ConfigService] Error saving config: {ex.Message}");
        }
    }

    public bool IsBackgroundOverlayEnabled() => _config.EnableBackgroundOverlay;
    public void SetEnableBackgroundOverlay(bool enabled) => Update(c => c.EnableBackgroundOverlay = enabled);

    public bool IsTaskbarAutoHideEnabled() => _config.EnableTaskbarAutoHide;
    public void SetEnableTaskbarAutoHide(bool enabled) => Update(c => c.EnableTaskbarAutoHide = enabled);

    public bool IsDisplaySyncEnabled() => _config.EnableDisplaySync;
    public void SetEnableDisplaySync(bool enabled) => Update(c => c.EnableDisplaySync = enabled);

    public bool ShouldShowExitTip() => _config.ShowExitTip;
    public void SetShowExitTip(bool show) => Update(c => c.ShowExitTip = show);
    
    public bool IsLaunchOnAppStartupEnabled() => _config.LaunchOnAppStartup;
    public void SetLaunchOnAppStartup(bool enabled) => Update(c => c.LaunchOnAppStartup = enabled);

    public bool IsLaunchOnTaskStartEnabled() => _config.LaunchOnTaskStart;
    public void SetLaunchOnTaskStart(bool enabled) => Update(c => c.LaunchOnTaskStart = enabled);

    public bool IsAutoStartFromThirdPartyEnabled() => _config.AutoStartFromThirdParty;
    public void SetAutoStartFromThirdParty(bool enabled) => Update(c => c.AutoStartFromThirdParty = enabled);

    public bool IsProtocolRegistrationEnabled() => _config.ProtocolRegistrationEnabled;
    public void SetProtocolRegistrationEnabled(bool enabled) => Update(c => c.ProtocolRegistrationEnabled = enabled);

    public bool IsAutoStartMonitoringOnProtocolLaunchEnabled() => _config.AutoStartMonitoringOnProtocolLaunch;
    public void SetAutoStartMonitoringOnProtocolLaunch(bool enabled) => Update(c => c.AutoStartMonitoringOnProtocolLaunch = enabled);

    public int GetWindowDetectionTimeout() => _config.WindowDetectionTimeout;
    public void SetWindowDetectionTimeout(int seconds) => Update(c => c.WindowDetectionTimeout = seconds);
    
    public BackgroundMode GetBackgroundMode() => _config.BackgroundMode;
    public string GetBackgroundColor() => _config.BackgroundColor;

    public void SetBackgroundMode(BackgroundMode mode)
    {
        _loggingService.AddLog($"[ConfigService] SetBackgroundMode: Current={_config.BackgroundMode}, New={mode}");
        if (_config.BackgroundMode == mode) return;
        Update(c => c.BackgroundMode = mode);
    }

    public void SetBackgroundColor(string color)
    {
        _loggingService.AddLog($"[ConfigService] SetBackgroundColor: Current={_config.BackgroundColor}, New={color}");
        if (string.Equals(_config.BackgroundColor, color, StringComparison.InvariantCultureIgnoreCase)) return;
        Update(c => c.BackgroundColor = color);
    }
    
    public string? GetAssociatedLaunchPath() => _config.AssociatedLaunchPath;
    public void SetAssociatedLaunchPath(string? path)
    {
        if (_config.AssociatedLaunchPath == path) return;
        Update(c => c.AssociatedLaunchPath = path);
    }
    
    public string GetDefaultProcessName() => _config.TargetProcessName;
    public void SetDefaultProcessName(string processName)
    {
        if (_config.TargetProcessName == processName) return;
        Update(c => c.TargetProcessName = processName);
    }
    public string? GetBackgroundImageFileName() => _config.BackgroundImageFileName;

    public LayoutProfile GetPortraitProfile() => ConvertToLayoutProfile(_config.Profiles.Portrait);
    public LayoutProfile GetLandscapeProfile() => ConvertToLayoutProfile(_config.Profiles.Landscape);

    public void SetBackgroundImageFileName(string? fileName)
    {
        if (_config.BackgroundImageFileName == fileName) return;
        Update(c => c.BackgroundImageFileName = fileName);
    }

    private AppConfig LoadOrCreateConfig()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            _loggingService.AddLogs("[ConfigService] Config file not found.", $"[ConfigService] Creating default at '{configPath}'.");
            var defaultConfig = CreateDefaultConfig();
            File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, AppJsonContext.Default.AppConfig));
            return defaultConfig;
        }

        try
        {
            _loggingService.AddLogs($"[ConfigService] Loading config from '{configPath}'.", "[ConfigService] Deserializing JSON...");
            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig)
                         ?? CreateDefaultConfig();

            return config;
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[ConfigService] Error loading config: {ex.Message}. Using default.");
            return CreateDefaultConfig();
        }
    }

    private AppConfig CreateDefaultConfig() => AppConfig.CreateDefault();

    private LayoutProfile ConvertToLayoutProfile(ProfileDefinition def)
    {
        return new LayoutProfile
        {
            Name = def.Name,
            Styles = ParseEnum<WindowStyles>(CollectionsMarshal.AsSpan(def.Styles)),
            ExStyles = ParseEnum<WindowExStyles>(CollectionsMarshal.AsSpan(def.ExStyles)),
            Sizing = def.Sizing,
            Positioning = def.Positioning,
            Display = def.Display
        };
    }
    
    private T ParseEnum<T>(ReadOnlySpan<string> values) where T : struct
    {
        if (values.IsEmpty) return default;

        uint rawValue = 0;
        foreach (var val in values)
        {
            rawValue |= Convert.ToUInt32(Enum.Parse(typeof(T), val, true));
        }

        return (T)(object)rawValue;
    }
    
}
