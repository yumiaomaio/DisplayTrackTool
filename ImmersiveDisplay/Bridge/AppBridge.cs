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
    /// Uses Switch Expression for high performance and conciseness (Native AOT compatible).
    /// </summary>
    public string HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("action", out var actionProp))
                return SerializeResponse("error", "Missing 'action' property.");

            string actionStr = actionProp.GetString() ?? "";
            string? callId = root.TryGetProperty("callId", out var cId) ? cId.GetString() : null;

            // --- Case-Insensitive Enum Parsing ---
            if (!Enum.TryParse<BridgeAction>(actionStr, true, out var action))
                action = BridgeAction.Unknown;
            
            return action switch
            {
                BridgeAction.GetInitialState => SerializeResponse("ok", GetInitialState(), callId),
                
                // --- Action Dispatching ---
                BridgeAction.StartMonitoring => Run(() => StartMonitoring(PString(root)), callId),
                BridgeAction.StopMonitoring  => Run(StopMonitoring, callId),
                
                BridgeAction.SetBackgroundColor      => Run(() => SetBackgroundColor(PString(root)), callId),
                BridgeAction.SetTargetProcessName    => Run(() => SetTargetProcessName(PString(root)), callId),
                BridgeAction.SetAssociatedLaunchPath => Run(() => SetAssociatedLaunchPath(PString(root)), callId),
                BridgeAction.SetBackgroundMode       => Run(() => SetBackgroundMode(PString(root)), callId),
                BridgeAction.SetWindowDetectionTimeout => Run(() => SetWindowDetectionTimeout(PInt(root, 10)), callId),
                
                BridgeAction.SetEnableTaskbarAutoHide   => Run(() => SetEnableTaskbarAutoHide(PBool(root)), callId),
                BridgeAction.SetEnableDisplaySync       => Run(() => SetEnableDisplaySync(PBool(root)), callId),
                BridgeAction.SetEnableBackgroundOverlay => Run(() => SetEnableBackgroundOverlay(PBool(root)), callId),
                BridgeAction.SetLaunchOnAppStartup      => Run(() => SetLaunchOnAppStartup(PBool(root)), callId),
                BridgeAction.SetLaunchOnTaskStart       => Run(() => SetLaunchOnTaskStart(PBool(root)), callId),
                BridgeAction.SetAutoStartFromThirdParty => Run(() => SetAutoStartFromThirdParty(PBool(root)), callId),
                BridgeAction.SetAutoStartMonitoringOnProtocolLaunch => Run(() => SetAutoStartMonitoringOnProtocolLaunch(PBool(root)), callId),
                BridgeAction.SetShowExitTip             => Run(() => SetShowExitTip(PBool(root)), callId),

                BridgeAction.SelectImage             => Run(SelectImage, callId),
                BridgeAction.ClearImage              => Run(ClearImage, callId),
                BridgeAction.SelectAssociatedProgram => Run(SelectAssociatedProgram, callId),
                BridgeAction.ClearLogs               => Run(ClearLogs, callId),
                BridgeAction.RestartAsAdmin          => Run(RestartAsAdmin, callId),
                BridgeAction.ExitApp                 => Run(ExitApp, callId),
                BridgeAction.ShowAbout               => Run(ShowAbout, callId),

                // --- Value Returning Actions ---
                BridgeAction.ShouldShowUacPrompt => SerializeResponse("ok", ShouldShowUacPrompt, callId),
                BridgeAction.RegisterProtocol   => SerializeResponse("ok", RegisterProtocol(), callId),
                BridgeAction.UnregisterProtocol => SerializeResponse("ok", UnregisterProtocol(), callId),
                BridgeAction.IsProtocolRegistered => SerializeResponse("ok", IsProtocolRegistered, callId),
                BridgeAction.IsAssociationValid => SerializeResponse("ok", IsAssociationValid(), callId),
                BridgeAction.CleanAssociation   => SerializeResponse("ok", CleanAssociation(), callId),
                BridgeAction.HandleAppProtocol  => Run(() => HandleAppProtocol(PString(root)), callId),

                BridgeAction.GetImageBase64         => SerializeResponse("ok", GetImageBase64(PString(root)), callId),
                BridgeAction.GetProcessCommandLine  => SerializeResponse("ok", GetProcessCommandLine(PString(root)), callId),
                BridgeAction.GetProcessIconBase64   => SerializeResponse("ok", GetProcessIconBase64(PString(root)), callId),
                BridgeAction.CheckProcessExists     => SerializeResponse("ok", CheckProcessExists(PString(root)), callId),
                BridgeAction.GetLogs                => SerializeResponse("ok", GetLogs(), callId),

                _ => SerializeResponse("error", $"Unknown action: {actionStr}", callId)
            };
        }
        catch (Exception ex)
        {
            return SerializeResponse("error", $"Exception: {ex.Message}");
        }
    }

    // --- Dispatch Helpers ---
    private string Run(Action act, string? callId) { act(); return SerializeResponse("ok", null, callId); }
    private string PString(JsonElement root) => root.TryGetProperty("payload", out var p) ? p.GetString() ?? "" : "";
    private bool PBool(JsonElement root) => root.TryGetProperty("payload", out var p) && p.GetBoolean();
    private int PInt(JsonElement root, int def) => root.TryGetProperty("payload", out var p) ? p.GetInt32() : def;

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
        _ = Task.Run(async () =>
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
        _ = Task.Run(async () =>
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
    public void SetShowExitTip(bool show) => configService.SetShowExitTip(show);
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
        dialogService.ShowInfo(
            "Responsive Window Tool\nVersion 1.2.0\n\nGitHub: https://github.com/yumiaomaio/GameWindowTool",
            "About");
    }

    public string[] GetLogs() => loggingService.Logs.ToArray();
    
    public void HandleAppProtocol(string uri)
    {
        loggingService.AddLog($"[AppBridge] App Protocol trigger received: {uri}");
        
        try 
        {
            if (string.IsNullOrEmpty(uri)) return;

            var uriObj = new Uri(uri);
            string scheme = uriObj.Scheme.ToLowerInvariant();
            string path = Uri.UnescapeDataString(uriObj.AbsolutePath).Trim();

            // 1. 处理 file:/// 协议
            if (scheme == "file")
            {
                if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && 
                    !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    loggingService.AddLog("[AppBridge] Non-executable file protocol ignored.");
                    return; // 3. 其它 file 直接丢弃
                }

                string finalPath = path;

                // 1. file 并且是 .lnk 就调用 ShortcutResolver.Resolve 解析
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    finalPath = ShortcutResolver.Resolve(path);
                    loggingService.AddLog($"[AppBridge] Resolved LNK to: {finalPath}");
                    
                    if (string.IsNullOrEmpty(finalPath) || !finalPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        loggingService.AddLog("[AppBridge] Resolved LNK does not point to a valid EXE. Discarding.");
                        return;
                    }
                }

                // 2. file 并且是 .exe 就保存路径
                SetAssociatedLaunchPath(finalPath);
                
                // 1, 2 调用 SetTargetProcessName
                string processName = Path.GetFileNameWithoutExtension(finalPath);
                if (!string.IsNullOrEmpty(processName)){ SetTargetProcessName(processName); }
                
            }
            // 4. 如果是 app:// 或者 http(s):// 就直接保存
            else if (scheme == "app" || scheme.StartsWith("http"))
            {
                loggingService.AddLog($"[AppBridge] Saving URI launch path: {uri}");
                SetAssociatedLaunchPath(uri);
                // 注意：URI 通常无法直接推断进程名，除非有额外配置
            }
            else
            {
                loggingService.AddLog($"[AppBridge] Unsupported scheme: {scheme}");
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[AppBridge] Protocol handling failed: {ex.Message}");
        }
    }
    // ... existing code ...
    
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
