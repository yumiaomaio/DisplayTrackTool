using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ImmersiveDisplay.Engine;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Components;

namespace ImmersiveDisplay.Bridge;

public partial class AppBridge(
    ITargetStateManager stateManager,
    IConfigService configService,
    ILoggingService loggingService,
    LaunchService launchService,
    AppIntegrationService appIntegrationService,
    AppProtocolHandler appProtocolHandler)
    : IDisposable
{
    public event Action<string>? OnMessageSent;

    public void Initialize()
    {
        configService.ConfigChanged += OnConfigChanged;
        stateManager.IsRunningChanged += OnIsRunningChanged;
        stateManager.WaitingCountdownChanged += OnWaitingCountdownChanged;
        loggingService.Logs.CollectionChanged += OnLogsChanged;
    }

    public void Dispose()
    {
        configService.ConfigChanged -= OnConfigChanged;
        stateManager.IsRunningChanged -= OnIsRunningChanged;
        stateManager.WaitingCountdownChanged -= OnWaitingCountdownChanged;
        loggingService.Logs.CollectionChanged -= OnLogsChanged;
    }

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

            if (!Enum.TryParse<BridgeAction>(actionStr, true, out var action))
                action = BridgeAction.Unknown;

            return action switch
            {
                BridgeAction.GetInitialState => SerializeResponse("ok", GetInitialState(), callId),

                BridgeAction.StartMonitoring => Run(() => StartMonitoring(PString(root)), callId),
                BridgeAction.StopMonitoring  => Run(StopMonitoring, callId),

                BridgeAction.SetBackgroundColor      => Run(() => configService.SetBackgroundColor(PString(root)), callId),
                BridgeAction.SetTargetProcessName    => Run(() => configService.SetDefaultProcessName(PString(root)), callId),
                BridgeAction.SetAssociatedLaunchPath => Run(() => configService.SetAssociatedLaunchPath(PString(root)), callId),
                BridgeAction.SetBackgroundMode       => Run(() => SetBackgroundMode(PString(root)), callId),
                BridgeAction.SetWindowDetectionTimeout => Run(() => configService.SetWindowDetectionTimeout(PInt(root, 10)), callId),

                BridgeAction.SetEnableTaskbarAutoHide   => Run(() => configService.SetEnableTaskbarAutoHide(PBool(root)), callId),
                BridgeAction.SetEnableDisplaySync       => Run(() => configService.SetEnableDisplaySync(PBool(root)), callId),
                BridgeAction.SetEnableBackgroundOverlay => Run(() => configService.SetEnableBackgroundOverlay(PBool(root)), callId),
                BridgeAction.SetLaunchOnAppStartup      => Run(() => configService.SetLaunchOnAppStartup(PBool(root)), callId),
                BridgeAction.SetLaunchOnTaskStart       => Run(() => configService.SetLaunchOnTaskStart(PBool(root)), callId),
                BridgeAction.SetAutoStartFromThirdParty => Run(() => configService.SetAutoStartFromThirdParty(PBool(root)), callId),
                BridgeAction.SetAutoStartMonitoringOnProtocolLaunch => Run(() => configService.SetAutoStartMonitoringOnProtocolLaunch(PBool(root)), callId),
                BridgeAction.SetShowExitTip             => Run(() => configService.SetShowExitTip(PBool(root)), callId),

                BridgeAction.SelectImage             => Run(SelectImage, callId),
                BridgeAction.ClearImage              => Run(() => configService.SetBackgroundImageFileName(null), callId),
                BridgeAction.SelectAssociatedProgram => Run(SelectAssociatedProgram, callId),
                BridgeAction.ClearLogs               => Run(() => loggingService.Logs.Clear(), callId),
                BridgeAction.RestartAsAdmin          => Run(PrivilegeHelper.RestartAsAdministrator, callId),
                BridgeAction.ExitApp                 => Run(() => Environment.Exit(0), callId),
                BridgeAction.ShowAbout               => Run(ShowAbout, callId),

                BridgeAction.ShouldShowUacPrompt => SerializeResponse("ok", appIntegrationService.ShouldShowUacPrompt, callId),
                BridgeAction.RegisterProtocol   => Run(() => { bool ok = ProtocolHelper.Register(); if (ok) configService.SetProtocolRegistrationEnabled(true); return ok; }, callId),
                BridgeAction.UnregisterProtocol => Run(() => { ProtocolHelper.Unregister(); configService.SetProtocolRegistrationEnabled(false); }, callId),
                BridgeAction.IsProtocolRegistered => SerializeResponse("ok", ProtocolHelper.IsRegistered(), callId),
                BridgeAction.IsAssociationValid => SerializeResponse("ok", ProtocolHelper.IsAssociationValid(), callId),
                BridgeAction.CleanAssociation   => SerializeResponse("ok", appProtocolHandler.CleanAssociation(), callId),
                BridgeAction.HandleAppProtocol  => Run(() => appProtocolHandler.HandleAppProtocol(PString(root)), callId),

                BridgeAction.SelectIconFile         => SerializeTypedResponse("ok", IconHelper.SelectAndCopyIcon(), callId, AppJsonContext.Default.IconImportResult),
                BridgeAction.ImportDroppedIcon      => SerializeTypedResponse("ok", ImportDroppedIcon(root), callId, AppJsonContext.Default.IconImportResult),
                BridgeAction.CreateAssociationUrls  => Run(() => appProtocolHandler.CreateAssociationUrls(PString(root)), callId),
                BridgeAction.QuickRegisterAssociation => SerializeResponse("ok", appProtocolHandler.QuickRegisterAssociation(), callId),
                BridgeAction.CreateDesktopShortcut  => SerializeResponse("ok", appProtocolHandler.CreateShareShortcut(), callId),

                BridgeAction.GetImageBase64         => SerializeResponse("ok", OverlayImageHelper.GetImageBase64(PString(root)), callId),
                BridgeAction.GetProcessCommandLine  => SerializeResponse("ok", GetProcessCommandLine(PString(root)), callId),
                BridgeAction.GetProcessIconBase64   => SerializeResponse("ok", ProcessHelper.GetProcessIconBase64(PString(root)), callId),
                BridgeAction.CheckProcessExists     => SerializeResponse("ok", ProcessHelper.GetProcessExecutablePath(PString(root)) != null, callId),
                BridgeAction.GetLogs                => SerializeResponse("ok", loggingService.Logs.TakeLast(50).ToArray(), callId),

                _ => SerializeResponse("error", $"Unknown action: {actionStr}", callId)
            };
        }
        catch (Exception ex)
        {
            return SerializeResponse("error", $"Exception: {ex.Message}");
        }
    }

    private string Run(Action act, string? callId) { act(); return SerializeResponse("ok", null, callId); }
    private string Run(Func<bool> func, string? callId) { bool result = func(); return SerializeResponse("ok", result, callId); }
    private static string PString(JsonElement root) => root.TryGetProperty("payload", out var p) ? p.GetString() ?? "" : "";
    private static bool PBool(JsonElement root) => root.TryGetProperty("payload", out var p) && p.GetBoolean();
    private static int PInt(JsonElement root, int def) => root.TryGetProperty("payload", out var p) ? p.GetInt32() : def;

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

    private string SerializeTypedResponse<T>(string status, T? result, string? callId, JsonTypeInfo<T> typeInfo)
    {
        string resultJson = result is null ? "null" : JsonSerializer.Serialize(result, typeInfo);
        string callIdJson = callId is null ? "null" : JsonSerializer.Serialize(callId, AppJsonContext.Default.String);
        return $"{{\"status\":{JsonSerializer.Serialize(status, AppJsonContext.Default.String)},\"result\":{resultJson},\"callId\":{callIdJson}}}";
    }

    private object GetInitialState()
    {
        return new InitialState
        {
            TargetProcessName = configService.GetDefaultProcessName() ?? "",
            IsRunning = stateManager.IsRunning,
            IsAdmin = PrivilegeHelper.IsAdministrator(),
            EnableTaskbarAutoHide = configService.IsTaskbarAutoHideEnabled(),
            EnableDisplaySync = configService.IsDisplaySyncEnabled(),
            EnableBackgroundOverlay = configService.IsBackgroundOverlayEnabled(),
            BackgroundMode = configService.GetBackgroundMode().ToString().ToLower(),
            CurrentImageFileName = configService.GetBackgroundImageFileName() ?? "",
            BackgroundColor = configService.GetBackgroundColor(),
            ShouldShowExitTip = configService.ShouldShowExitTip(),
            AssociatedLaunchPath = configService.GetAssociatedLaunchPath() ?? "",
            LaunchOnAppStartup = configService.IsLaunchOnAppStartupEnabled(),
            LaunchOnTaskStart = configService.IsLaunchOnTaskStartEnabled(),
            AutoStartFromThirdParty = configService.IsAutoStartFromThirdPartyEnabled(),
            AutoStartMonitoringOnProtocolLaunch = configService.IsAutoStartMonitoringOnProtocolLaunchEnabled(),
            ShouldShowUacPrompt = appIntegrationService.ShouldShowUacPrompt,
            IsProtocolRegistered = ProtocolHelper.IsRegistered(),
            WaitingCountdown = stateManager.WaitingCountdown,
            WindowDetectionTimeout = configService.GetWindowDetectionTimeout(),
            Logs = loggingService.Logs.ToArray()
        };
    }

    private void OnConfigChanged(AppConfig config)
    {
        PushToFrontend(config, AppJsonContext.Default.AppConfig);
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
}
