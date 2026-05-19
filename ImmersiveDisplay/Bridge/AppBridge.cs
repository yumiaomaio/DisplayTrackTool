using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text.Json;
using ImmersiveDisplay.Services;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay.Bridge;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
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
    private CoreWebView2? _webView;
    private readonly JsonSerializerOptions _jsonOptions = new()
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase 
    };

    /// <summary>
    /// Binds the bridge to the WebView and starts automatic state synchronization.
    /// </summary>
    public void Initialize(CoreWebView2 webView)
    {
        _webView = webView;
        
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
        _webView = null;
    }

    private void OnConfigChanged(string key, object? value)
    {
        PushToFrontend(new Dictionary<string, object?> { { key, value } });
    }

    private void OnIsRunningChanged(bool isRunning)
    {
        PushToFrontend(new Dictionary<string, object?> { { nameof(IsRunning), isRunning } });
    }

    private void OnWaitingCountdownChanged(int countdown)
    {
        PushToFrontend(new Dictionary<string, object?> { { nameof(WaitingCountdown), countdown } });
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PushToFrontend(new { Logs = loggingService.Logs });
    }

    private void PushToFrontend(object state)
    {
        if (_webView == null) return;
        try
        {
            string json = JsonSerializer.Serialize(state, _jsonOptions);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () => 
            {
                try
                {
                    if (_webView != null)
                    {
                        await _webView.ExecuteScriptAsync($"window.onStateChanged({json})");
                    }
                }
                catch (Exception ex)
                {
                    loggingService.AddLog($"[AppBridge] JS eval failed: {ex.Message}");
                }
            }));
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

    // --- Actions called from JS ---
    public void StartMonitoring(string processName)
    {
        configService.SetDefaultProcessName(processName);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () => 
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
        }));
    }

    public void StopMonitoring()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () => 
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
        }));
    }

    public void SetBackgroundColor(string color) => configService.SetBackgroundColor(color);
    public void SetEnableTaskbarAutoHide(bool enable) => configService.SetEnableTaskbarAutoHide(enable);
    public void SetEnableDisplaySync(bool enable) => configService.SetEnableDisplaySync(enable);
    public void SetEnableBackgroundOverlay(bool enable) => configService.SetEnableBackgroundOverlay(enable);
    public void SetShowExitTip(bool show) => configService.SetShowExitTip(show);
    public void SetAssociatedLaunchPath(string path) => configService.SetAssociatedLaunchPath(path);
    public void SetLaunchOnAppStartup(bool enable) => configService.SetLaunchOnAppStartup(enable);
    public void SetLaunchOnTaskStart(bool enable) => configService.SetLaunchOnTaskStart(enable);
    
    public void SetAutoStartFromThirdParty(bool enable)
    {
        if (enable)
        {
            if (!protocolService.IsAssociationValid())
            {
                protocolService.Register();
            }
            configService.SetAutoStartFromThirdParty(true);
        }
        else
        {
            configService.SetAutoStartFromThirdParty(false);
        }
    }

    public void SetAutoStartMonitoringOnProtocolLaunch(bool enable) => configService.SetAutoStartMonitoringOnProtocolLaunch(enable);
    public void SetWindowDetectionTimeout(int seconds) => configService.SetWindowDetectionTimeout(seconds);

    public void CleanAssociation()
    {
        protocolService.Unregister();
        configService.SetAutoStartFromThirdParty(false);
        loggingService.AddLog("> System associations (Protocol & Shortcuts) cleaned successfully.");
    }

    public void SelectAssociatedProgram() => appIntegrationService.SelectAssociatedProgram();
    public void SelectImage() => overlayImageService.SelectAndSetBackgroundImage();

    public void ClearImage()
    {
        configService.SetBackgroundMode(Models.BackgroundMode.SOLID_COLOR);
        configService.SetBackgroundImageFileName(null);
    }

    public string GetImageBase64(string fileName) => overlayImageService.GetImageBase64(fileName);
    public string GetProcessCommandLine(string processName) => processService.GetProcessCommandLine(processName) ?? "";
    public string GetProcessIconBase64(string processName) => processService.GetProcessIconBase64(processName);
    public bool CheckProcessExists(string processName) => processService.GetProcessExecutablePath(processName) != null;
    public void RestartAsAdmin() => privilegeService.RestartAsAdministrator();
    public void ExitApp() => System.Windows.Application.Current.Shutdown();
    public void ShowAbout()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => 
        {
            dialogService.ShowInfo(
                "Responsive Window Tool\nVersion 1.2.0\n\n \n\nGitHub: https://github.com/yumiaomaio/GameWindowTool", 
                "About");
        }));
    }

    public string[] GetLogs() => loggingService.Logs.ToArray();

    // --- Inner Helpers ---
    private bool CalculateShouldShowUacPrompt()
    {
        if (IsAdmin) return false;
        if (!App.IsProtocolAutoStart) return true;
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
