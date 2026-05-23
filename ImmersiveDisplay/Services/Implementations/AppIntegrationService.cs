using ImmersiveDisplay.Helpers;

namespace ImmersiveDisplay.Services.Implementations;

public class AppIntegrationService(
    IKeyboardHookService keyboardHook,
    ITargetStateManager stateManager,
    IConfigService configService,
    ILoggingService loggingService,
    ILaunchService launchService)
    : IAppIntegrationService
{
    public bool IsProtocolAutoStart { get; set; }

    public void InitializeHooksAndTriggers()
    {
        // Global shortcut log redirection
        ShortcutResolver.LogAction = (msg) => loggingService.AddLog(msg);

        // Subscribe to keyboard hooks
        keyboardHook.KeyPressed += (vkCode) =>
        {
            const int vkF9 = 0x78;
            const int vkF12 = 0x7B;

            if (vkCode == vkF9)
            {
                var processName = configService.GetDefaultProcessName();
                if (!stateManager.IsRunning && !string.IsNullOrWhiteSpace(processName))
                {
                    loggingService.AddLog("F9 key pressed. Starting...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await stateManager.StartAsync(processName);
                        }
                        catch (Exception ex)
                        {
                            loggingService.AddLog($"Failed to start from F9 hook: {ex.Message}");
                        }
                    });
                }
            }
            else if (vkCode == vkF12)
            {
                if (stateManager.IsRunning)
                {
                    loggingService.AddLog("F12 key pressed. Shutting down...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await stateManager.StopAsync();
                            launchService.ClearHistory();
                        }
                        catch (Exception ex)
                        {
                            loggingService.AddLog($"Failed to stop from F12 hook: {ex.Message}");
                        }
                    });
                }
            }
        };
    }

    public void ExecuteStartupLogic()
    {
        bool autoStartedByThirdParty = false;

        // Check for path updates if feature is enabled
        if (configService.IsAutoStartFromThirdPartyEnabled())
        {
            if (!ProtocolHelper.IsAssociationValid())
            {
                loggingService.AddLog("[ProtocolHelper] Association invalid or Start Menu shortcut missing. Restoring associations...");
                if (ProtocolHelper.Register())
                    loggingService.AddLog("[ProtocolHelper] Protocol and shortcuts registered.");
                else
                    loggingService.AddLog("[ProtocolHelper] Failed to register protocol.");
            }
        }

        if (configService.IsAutoStartFromThirdPartyEnabled() && IsProtocolAutoStart)
        {
            loggingService.AddLog($"[Startup] Third-party launcher detected via protocol. Auto-launching target program.");
            autoStartedByThirdParty = true;

            // 1. Launch associated program
            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                launchService.Launch(path);
            }

            // 2. Start monitoring task conditionally
            if (configService.IsAutoStartMonitoringOnProtocolLaunchEnabled())
            {
                bool isExe = IsAssociatedPathExe();
                bool isAdmin = PrivilegeHelper.IsAdministrator();

                if (isExe || isAdmin)
                {
                    loggingService.AddLog($"[Startup] Auto-start monitoring active. (Bypass UAC or Admin confirmed). Starting monitoring.");
                    var targetProc = configService.GetDefaultProcessName();
                    if (!stateManager.IsRunning && !string.IsNullOrWhiteSpace(targetProc))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await stateManager.StartAsync(targetProc);
                            }
                            catch (Exception ex)
                            {
                                loggingService.AddLog($"Startup monitoring failed: {ex.Message}");
                            }
                        });
                    }
                }
                else
                {
                    loggingService.AddLog($"[Startup] Auto-start monitoring blocked: Standard user running a URL protocol launch. UAC warning prompted.");
                }
            }
            else
            {
                loggingService.AddLog($"[Startup] Auto-start monitoring disabled by default settings. Opening in standby mode.");
            }
        }

        // --- Associated Launch (App Startup) ---
        if (!autoStartedByThirdParty && configService.IsLaunchOnAppStartupEnabled())
        {
            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                launchService.Launch(path);
            }
        }
    }

    private bool IsAssociatedPathExe()
    {
        var path = configService.GetAssociatedLaunchPath()?.Trim();
        if (string.IsNullOrWhiteSpace(path)) return false;

        var cleanPath = path.Trim('\"').Trim();
        if (cleanPath.Contains("://") || cleanPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    public void SelectAssociatedProgram()
    {
        var path = NativeDialogHelper.ShowOpenFileDialog(
            "Select Application or Shortcut",
            "Applications & Shortcuts|*.exe;*.lnk;*.url|All files (*.*)|*.*");

        if (path != null)
        {
            string resolvedPath;
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = ShortcutResolver.Resolve(path);
            }
            else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = ShortcutResolver.ResolveUrl(path);
            }
            else
            {
                resolvedPath = path.Contains(' ') ? $"\"{path}\"" : path;
            }
            configService.SetAssociatedLaunchPath(resolvedPath);
        }
    }
}
