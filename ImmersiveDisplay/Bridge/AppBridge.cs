// File: Bridge/AppBridge.cs

using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Bridge;

/// <summary>
/// A pure C# bridge that handles logic routing and state synchronization.
/// Decoupled from WebView2 and COM for Native AOT compatibility.
/// </summary>
public class AppBridge(
    ITargetStateManager stateManager,
    IConfigService configService,
    ILoggingService loggingService,
    IProcessService processService,
    IProtocolService protocolService,
    ILaunchService launchService,
    IPrivilegeService privilegeService,
    IDialogService dialogService,
    IAppIntegrationService appIntegrationService,
    IOverlayImageService overlayImageService)
    : IDisposable
{
    /// <summary>
    /// Event triggered when the DLL wants to push a state update or message to the frontend.
    /// The host application should listen to this and relay it via webView.PostWebMessageAsJson.
    /// </summary>
    public event Action<string>? OnMessageSent;

    public void Initialize()
    {
        // --- Reactive Subscriptions to Service Events ---
        configService.ConfigChanged += OnConfigChanged;
        stateManager.IsRunningChanged += OnIsRunningChanged;
        stateManager.WaitingCountdownChanged += OnWaitingCountdownChanged;
        loggingService.Logs.CollectionChanged += OnLogsChanged;
    }

    public void Dispose()
    {
        // --- Unsubscribe to prevent memory leaks ---
        configService.ConfigChanged -= OnConfigChanged;
        stateManager.IsRunningChanged -= OnIsRunningChanged;
        stateManager.WaitingCountdownChanged -= OnWaitingCountdownChanged;
        loggingService.Logs.CollectionChanged -= OnLogsChanged;
    }

    /// <summary>
    /// Main entry point for messages coming from the frontend (via the host application).
    /// Hardcoded router for Native AOT compatibility (avoiding Reflection).
    /// </summary>
    public string HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("action", out var actionProp))
                return SerializeResponse("error", "Missing 'action' property.");

            string action = actionProp.GetString() ?? "";
            string? callId = root.TryGetProperty("callId", out var cId) ? cId.GetString() : null;
            
            // Dispatch based on action name
            switch (action)
            {
                case "GetInitialState":
                    return SerializeResponse("ok", GetInitialState(), callId);

                case "StartMonitoring":
                    StartMonitoring(root.TryGetProperty("payload", out var p1) ? p1.GetString() ?? "" : "");
                    return SerializeResponse("ok", null, callId);

                case "StopMonitoring":
                    StopMonitoring();
                    return SerializeResponse("ok", null, callId);

                case "SetBackgroundColor":
                    SetBackgroundColor(root.TryGetProperty("payload", out var p2) ? p2.GetString() ?? "" : "");
                    return SerializeResponse("ok", null, callId);

                case "SetTargetProcessName":
                    SetTargetProcessName(root.TryGetProperty("payload", out var p3) ? p3.GetString() ?? "" : "");
                    return SerializeResponse("ok", null, callId);

                case "SetAssociatedLaunchPath":
                    SetAssociatedLaunchPath(root.TryGetProperty("payload", out var p4) ? p4.GetString() ?? "" : "");
                    return SerializeResponse("ok", null, callId);

                case "SetEnableTaskbarAutoHide":
                    SetEnableTaskbarAutoHide(root.TryGetProperty("payload", out var p5) && p5.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetEnableDisplaySync":
                    SetEnableDisplaySync(root.TryGetProperty("payload", out var p6) && p6.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetEnableBackgroundOverlay":
                    SetEnableBackgroundOverlay(root.TryGetProperty("payload", out var p7) && p7.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetBackgroundMode":
                    SetBackgroundMode(root.TryGetProperty("payload", out var p8) ? p8.GetString() ?? "" : "");
                    return SerializeResponse("ok", null, callId);

                case "SelectImage":
                    SelectImage();
                    return SerializeResponse("ok", null, callId);

                case "ClearImage":
                    ClearImage();
                    return SerializeResponse("ok", null, callId);

                case "SelectAssociatedProgram":
                    SelectAssociatedProgram();
                    return SerializeResponse("ok", null, callId);

                case "SetLaunchOnAppStartup":
                    SetLaunchOnAppStartup(root.TryGetProperty("payload", out var p9) && p9.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetLaunchOnTaskStart":
                    SetLaunchOnTaskStart(root.TryGetProperty("payload", out var p10) && p10.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetAutoStartFromThirdParty":
                    SetAutoStartFromThirdParty(root.TryGetProperty("payload", out var p11) && p11.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetAutoStartMonitoringOnProtocolLaunch":
                    SetAutoStartMonitoringOnProtocolLaunch(root.TryGetProperty("payload", out var p12) && p12.GetBoolean());
                    return SerializeResponse("ok", null, callId);

                case "SetWindowDetectionTimeout":
                    SetWindowDetectionTimeout(root.TryGetProperty("payload", out var p13) ? p13.GetInt32() : 10);
                    return SerializeResponse("ok", null, callId);

                case "RegisterProtocol":
                    return SerializeResponse("ok", RegisterProtocol(), callId);

                case "UnregisterProtocol":
                    return SerializeResponse("ok", UnregisterProtocol(), callId);

                case "IsAssociationValid":
                    return SerializeResponse("ok", IsAssociationValid(), callId);

                case "CleanAssociation":
                    return SerializeResponse("ok", CleanAssociation(), callId);

                case "ClearLogs":
                    ClearLogs();
                    return SerializeResponse("ok", null, callId);

                case "GetImageBase64":
                    return SerializeResponse("ok", GetImageBase64(root.TryGetProperty("payload", out var p14) ? p14.GetString() ?? "" : ""), callId);

                case "GetProcessCommandLine":
                    return SerializeResponse("ok", GetProcessCommandLine(root.TryGetProperty("payload", out var p15) ? p15.GetString() ?? "" : ""), callId);

                case "GetProcessIconBase64":
                    return SerializeResponse("ok", GetProcessIconBase64(root.TryGetProperty("payload", out var p16) ? p16.GetString() ?? "" : ""), callId);

                case "CheckProcessExists":
                    return SerializeResponse("ok", CheckProcessExists(root.TryGetProperty("payload", out var p17) ? p17.GetString() ?? "" : ""), callId);

                case "RestartAsAdmin":
                    RestartAsAdmin();
                    return SerializeResponse("ok", null, callId);

                case "ExitApp":
                    ExitApp();
                    return SerializeResponse("ok", null, callId);

                case "ShowAbout":
                    ShowAbout();
                    return SerializeResponse("ok", null, callId);

                case "GetLogs":
                    return SerializeResponse("ok", GetLogs(), callId);

                default:
                    return SerializeResponse("error", $"Unknown action: {action}", callId);
            }
        }
        catch (Exception ex)
        {
            return SerializeResponse("error", $"Exception: {ex.Message}");
        }
    }

    private string SerializeResponse(string status, object? result = null, string? callId = null)
    {
        var response = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["result"] = result,
            ["callId"] = callId
        };
        return JsonSerializer.Serialize(response, AppJsonContext.Default.DictionaryStringObject);
    }

    private object GetInitialState()
    {
        return new Dictionary<string, object?>
        {
            ["targetProcessName"] = TargetProcessName,
            ["isRunning"] = IsRunning,
            ["isAdmin"] = IsAdmin,
            ["enableTaskbarAutoHide"] = EnableTaskbarAutoHide,
            ["enableDisplaySync"] = EnableDisplaySync,
            ["enableBackgroundOverlay"] = EnableBackgroundOverlay,
            ["backgroundMode"] = BackgroundMode,
            ["currentImageFileName"] = CurrentImageFileName,
            ["backgroundColor"] = BackgroundColor,
            ["shouldShowExitTip"] = ShouldShowExitTip,
            ["associatedLaunchPath"] = AssociatedLaunchPath,
            ["launchOnAppStartup"] = LaunchOnAppStartup,
            ["launchOnTaskStart"] = LaunchOnTaskStart,
            ["autoStartFromThirdParty"] = AutoStartFromThirdParty,
            ["autoStartMonitoringOnProtocolLaunch"] = AutoStartMonitoringOnProtocolLaunch,
            ["shouldShowUacPrompt"] = ShouldShowUacPrompt,
            ["isProtocolRegistered"] = IsProtocolRegistered,
            ["waitingCountdown"] = WaitingCountdown,
            ["windowDetectionTimeout"] = WindowDetectionTimeout,
            ["logs"] = GetLogs()
        };
    }

    private void OnConfigChanged(string key, object? value)
    {
        PushToFrontend(new Dictionary<string, object?> { { key, value } }, AppJsonContext.Default.DictionaryStringObject);
    }

    private void OnIsRunningChanged(bool isRunning)
    {
        PushToFrontend(new Dictionary<string, object?> { { nameof(IsRunning), isRunning } }, AppJsonContext.Default.DictionaryStringObject);
    }

    private void OnWaitingCountdownChanged(int countdown)
    {
        PushToFrontend(new Dictionary<string, object?> { { nameof(WaitingCountdown), countdown } }, AppJsonContext.Default.DictionaryStringObject);
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PushToFrontend(new FrontendLogsDto { Logs = loggingService.Logs.ToArray() }, AppJsonContext.Default.FrontendLogsDto);
    }

    private void PushToFrontend<T>(T state, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            string json = JsonSerializer.Serialize(state, typeInfo);
            OnMessageSent?.Invoke(json);
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[AppBridge] State push serialization failed: {ex.Message}");
        }
    }

    // --- Stateless Properties mapping directly to Services ---
    public string TargetProcessName => configService.GetDefaultProcessName() ?? "";
    public bool IsRunning => stateManager.IsRunning;
    public bool IsAdmin => privilegeService.IsAdministrator();
    public bool EnableTaskbarAutoHide => configService.IsTaskbarAutoHideEnabled();
    public bool EnableDisplaySync => configService.IsDisplaySyncEnabled();
    public bool EnableBackgroundOverlay => configService.IsBackgroundOverlayEnabled();
    public string BackgroundMode => configService.GetBackgroundMode().ToString().ToLower();
    public string CurrentImageFileName => configService.GetBackgroundImageFileName() ?? "";
    public string BackgroundColor => configService.GetBackgroundColor();
    public bool ShouldShowExitTip => configService.ShouldShowExitTip();
    public string AssociatedLaunchPath => configService.GetAssociatedLaunchPath() ?? "";
    public bool LaunchOnAppStartup => configService.IsLaunchOnAppStartupEnabled();
    public bool LaunchOnTaskStart => configService.IsLaunchOnTaskStartEnabled();
    public bool AutoStartFromThirdParty => configService.IsAutoStartFromThirdPartyEnabled();
    public bool AutoStartMonitoringOnProtocolLaunch => configService.IsAutoStartMonitoringOnProtocolLaunchEnabled();
    public bool ShouldShowUacPrompt => CalculateShouldShowUacPrompt();
    public bool IsProtocolRegistered => protocolService.IsRegistered();
    public int WaitingCountdown => stateManager.WaitingCountdown;
    public int WindowDetectionTimeout => configService.GetWindowDetectionTimeout();

    // --- Actions ---
    public void StartMonitoring(string processName)
    {
        configService.SetDefaultProcessName(processName);
        UiDispatcher.BeginInvoke(async () => 
        {
            try
            {
                await stateManager.StartAsync(processName);
            }
            catch (Exception ex)
            {
                loggingService.AddLog($"Failed to start monitoring: {ex.Message}");
                dialogService.ShowError($"An error occurred: {ex.Message}");
            }
        });
    }

    public void StopMonitoring()
    {
        UiDispatcher.BeginInvoke(async () => 
        {
            try
            {
                await stateManager.StopAsync();
                launchService.ClearHistory();
            }
            catch (Exception ex)
            {
                loggingService.AddLog($"Error during stop: {ex.Message}");
            }
        });
    }

    public void SetBackgroundColor(string color) => configService.SetBackgroundColor(color);
    public void SetTargetProcessName(string processName) => configService.SetDefaultProcessName(processName);
    public void SetAssociatedLaunchPath(string path) => configService.SetAssociatedLaunchPath(path);
    public void SetEnableTaskbarAutoHide(bool enable) => configService.SetEnableTaskbarAutoHide(enable);
    public void SetEnableDisplaySync(bool enable) => configService.SetEnableDisplaySync(enable);
    public void SetEnableBackgroundOverlay(bool enable) => configService.SetEnableBackgroundOverlay(enable);
    public void SetBackgroundMode(string mode)
    {
        loggingService.AddLog($"[AppBridge] SetBackgroundMode called with: {mode}");
        BackgroundMode? targetMode = null;
        if (mode.Equals("color", StringComparison.OrdinalIgnoreCase)) targetMode = Models.BackgroundMode.COLOR;
        else if (mode.Equals("image", StringComparison.OrdinalIgnoreCase)) targetMode = Models.BackgroundMode.IMAGE;
        else if (Enum.TryParse<BackgroundMode>(mode, true, out var result)) targetMode = result;

        if (targetMode.HasValue)
        {
            loggingService.AddLog($"[AppBridge] Mapping '{mode}' to enum {targetMode.Value}.");
            configService.SetBackgroundMode(targetMode.Value);
        }
    }
    public void SelectImage() => overlayImageService.SelectAndSetBackgroundImage();
    public void ClearImage() => overlayImageService.ClearImage();
    public void SelectAssociatedProgram() => appIntegrationService.SelectAssociatedProgram();
    public void SetLaunchOnAppStartup(bool enable) => configService.SetLaunchOnAppStartup(enable);
    public void SetLaunchOnTaskStart(bool enable) => configService.SetLaunchOnTaskStart(enable);
    public void SetAutoStartFromThirdParty(bool enable) => configService.SetAutoStartFromThirdParty(enable);
    public void SetAutoStartMonitoringOnProtocolLaunch(bool enable) => configService.SetAutoStartMonitoringOnProtocolLaunch(enable);
    public void SetWindowDetectionTimeout(int seconds) => configService.SetWindowDetectionTimeout(seconds);
    public bool RegisterProtocol() => protocolService.Register();
    public bool UnregisterProtocol() => protocolService.Unregister();
    public bool IsAssociationValid() => protocolService.IsAssociationValid();
    
    public bool CleanAssociation()
    {
        bool success = protocolService.Unregister();
        configService.SetAutoStartFromThirdParty(false);
        return success;
    }

    public void ClearLogs() => loggingService.Logs.Clear();
    public void SaveConfig() { }

    public string GetImageBase64(string fileName) => overlayImageService.GetImageBase64(fileName);
    public string GetProcessCommandLine(string processName) => processService.GetProcessCommandLine(processName) ?? "";
    public string GetProcessIconBase64(string processName) => processService.GetProcessIconBase64(processName);
    public bool CheckProcessExists(string processName) => processService.GetProcessExecutablePath(processName) != null;
    public void RestartAsAdmin() => privilegeService.RestartAsAdministrator();
    public void ExitApp() => Environment.Exit(0);
    public void ShowAbout()
    {
        UiDispatcher.BeginInvoke(() => 
        {
            dialogService.ShowInfo(
                "Responsive Window Tool\nVersion 1.2.0\n\nGitHub: https://github.com/yumiaomaio/GameWindowTool", 
                "About");
        });
    }

    public string[] GetLogs() => loggingService.Logs.ToArray();

    private bool CalculateShouldShowUacPrompt()
    {
        if (IsAdmin) return false;
        if (!appIntegrationService.IsProtocolAutoStart) return true;
        if (AutoStartFromThirdParty)
        {
            if (AutoStartMonitoringOnProtocolLaunch)
            {
                if (IsAssociatedPathExe())
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool IsAssociatedPathExe()
    {
        var path = AssociatedLaunchPath?.Trim();
        if (string.IsNullOrWhiteSpace(path)) return false;
        var cleanPath = path.Trim('\"').Trim();
        if (cleanPath.Contains("://") || cleanPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }
}
