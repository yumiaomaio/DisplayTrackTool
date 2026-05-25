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
    ILaunchService launchService,
    IAppIntegrationService appIntegrationService)
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
                BridgeAction.SelectAssociatedProgram => Run(appIntegrationService.SelectAssociatedProgram, callId),
                BridgeAction.ClearLogs               => Run(() => loggingService.Logs.Clear(), callId),
                BridgeAction.RestartAsAdmin          => Run(PrivilegeHelper.RestartAsAdministrator, callId),
                BridgeAction.ExitApp                 => Run(() => Environment.Exit(0), callId),
                BridgeAction.ShowAbout               => Run(ShowAbout, callId),

                // --- Value Returning Actions ---
                BridgeAction.ShouldShowUacPrompt => SerializeResponse("ok", appIntegrationService.ShouldShowUacPrompt, callId),
                BridgeAction.RegisterProtocol   => SerializeResponse("ok", ProtocolHelper.Register(), callId),
                BridgeAction.UnregisterProtocol => SerializeResponse("ok", ProtocolHelper.Unregister(), callId),
                BridgeAction.IsProtocolRegistered => SerializeResponse("ok", ProtocolHelper.IsRegistered(), callId),
                BridgeAction.IsAssociationValid => SerializeResponse("ok", ProtocolHelper.IsAssociationValid(), callId),
                BridgeAction.CleanAssociation   => SerializeResponse("ok", CleanAssociation(), callId),
                BridgeAction.HandleAppProtocol  => Run(() => HandleAppProtocol(PString(root)), callId),

                // --- Icon & URL Registration ---
                BridgeAction.SelectIconFile         => SerializeTypedResponse("ok", IconHelper.SelectAndCopyIcon(), callId, AppJsonContext.Default.IconImportResult),
                BridgeAction.ImportDroppedIcon      => SerializeTypedResponse("ok", ImportDroppedIcon(root), callId, AppJsonContext.Default.IconImportResult),
                BridgeAction.CreateAssociationUrls  => Run(() => CreateAssociationUrls(PString(root)), callId),
                BridgeAction.QuickRegisterAssociation => SerializeResponse("ok", QuickRegisterAssociation(), callId),
                BridgeAction.CleanAllAssociationUrls => SerializeResponse("ok", CleanAllAssociationUrls(), callId),
                BridgeAction.CreateDesktopShortcut  => SerializeResponse("ok", CreateShareShortcut(), callId),

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

    // --- Stateless Properties mapping directly to Services ---
    public bool IsRunning => stateManager.IsRunning;
    public int WaitingCountdown => stateManager.WaitingCountdown;

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
                NativeDialogHelper.ShowError($"An error occurred: {ex.Message}");
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

    public void SelectImage()
    {
        var path = NativeDialogHelper.ShowOpenFileDialog(
            "Select a Background Image",
            "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*");

        if (path != null)
        {
            string? fileName = OverlayImageHelper.CopyToBackgrounds(path);
            if (fileName != null)
            {
                configService.SetBackgroundMode(Models.BackgroundMode.IMAGE);
                configService.SetBackgroundImageFileName(fileName);
            }
            else
            {
                NativeDialogHelper.ShowError("Failed to copy image to backgrounds folder.");
            }
        }
    }

    public bool CleanAssociation()
    {
        bool cleaned = ProtocolHelper.CleanAllAssociationUrls();
        configService.SetAutoStartFromThirdParty(false);
        return cleaned;
    }

    private IconImportResult? ImportDroppedIcon(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var p)) return null;
        string? fileName = null;
        string? base64 = null;
        if (p.TryGetProperty("fileName", out var fn)) fileName = fn.GetString();
        if (p.TryGetProperty("data", out var d)) base64 = d.GetString();
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(base64)) return null;

        return IconHelper.SaveIconFromBase64(fileName, base64);
    }

    private void CreateAssociationUrls(string json)
    {
        var request = JsonSerializer.Deserialize(json, AppJsonContext.Default.CreateAssociationRequest);
        if (request?.Entries == null) return;
        ProtocolHelper.CreateMultipleUrlShortcuts(request.Entries, request.IconFileName);
    }

    private bool QuickRegisterAssociation()
    {
        try
        {
            // Ensure protocol is registered
            if (!ProtocolHelper.IsRegistered())
                ProtocolHelper.Register();

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string iconLine = string.IsNullOrEmpty(exePath) ? "" : $"\r\nIconIndex=0\r\nIconFile={exePath}";
            string content = $"[InternetShortcut]\r\nURL=immersivedisplay://autostart{iconLine}";

            string startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            Directory.CreateDirectory(startMenuDir);
            File.WriteAllText(Path.Combine(startMenuDir, "Immersive Auto Launch.url"), content);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CleanAllAssociationUrls()
    {
        return ProtocolHelper.CleanAllAssociationUrls();
    }

    private bool CreateShareShortcut()
    {
        var path = configService.GetAssociatedLaunchPath();
        if (string.IsNullOrWhiteSpace(path)) return false;

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string shortcutPath = Path.Combine(desktop, $"{Path.GetFileNameWithoutExtension(path)}.lnk");
        return ShortcutResolver.CreateLnk(shortcutPath, path);
    }

    public string GetProcessCommandLine(string processName)
    {
        string? commandLine = ProcessHelper.GetProcessCommandLine(processName, out bool permissionDenied);
        
        if (permissionDenied)
        {
            loggingService.AddLog($"[AppBridge] Command line detection failed (Permission Denied) for '{processName}'.");
            NativeDialogHelper.ShowWarning(
                "权限不足，无法获取目标进程的启动命令行参数（Launch Arguments）。\n\n" +
                "当前已自动降级为仅获取程序执行文件路径。若要抓取完整的启动参数（如 Steam 或 Epic 游戏的特殊启动参数），请以【管理员身份】重新运行本工具。\n\n" +
                "-----------------------------------------\n\n" +
                "Insufficient permissions to capture process startup arguments.\n\n" +
                "Falling back to executable path only. To capture complete launch parameters (e.g. for Steam/Epic games), please restart this tool as Administrator.",
                "权限提示 / Permission Warning");
        }

        return commandLine ?? "";
    }
    
    public void ShowAbout()
    {
        NativeDialogHelper.ShowInfo(
            "Responsive Window Tool\nVersion 1.2.0\n\nGitHub: https://github.com/yumiaomaio/GameWindowTool",
            "About");
    }
    
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
                configService.SetAssociatedLaunchPath(finalPath);

                // 1, 2 调用 SetTargetProcessName
                string processName = Path.GetFileNameWithoutExtension(finalPath);
                if (!string.IsNullOrEmpty(processName)){ configService.SetDefaultProcessName(processName); }
                
            }
            // 4. 如果是 app:// 或者 http(s):// 就直接保存
            else if (scheme == "app" || scheme.StartsWith("http"))
            {
                loggingService.AddLog($"[AppBridge] Saving URI launch path: {uri}");
                configService.SetAssociatedLaunchPath(uri);
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
}
