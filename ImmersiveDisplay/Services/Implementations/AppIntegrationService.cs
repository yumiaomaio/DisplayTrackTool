using System.Diagnostics;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;

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

    public bool ShouldShowUacPrompt
    {
        get
        {
            if (PrivilegeHelper.IsAdministrator()) return false;
            if (!IsProtocolAutoStart) return true;
            if (configService.IsAutoStartFromThirdPartyEnabled() &&
                configService.IsAutoStartMonitoringOnProtocolLaunchEnabled() &&
                IsAssociatedPathExe())
                return false;
            return true;
        }
    }

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
            else if (vkCode == 0x73 /* VK_F4 */ && stateManager.IsRunning && IsProtocolAutoStart)
            {
                loggingService.AddLog("F4 key pressed. Terminating target and exiting...");
                var hwnd = stateManager.CurrentTargetHwnd;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await stateManager.StopAsync();

                        if (hwnd.HasValue)
                        {
                            NativeMethods.GetWindowThreadProcessId(hwnd.Value, out uint pid);
                            if (pid != 0)
                            {
                                loggingService.AddLog($"Terminating target process (PID: {pid})...");
                                try
                                {
                                    using var process = Process.GetProcessById((int)pid);
                                    if (!process.HasExited)
                                    {
                                        process.CloseMainWindow();
                                        if (!process.WaitForExit(3000))
                                        {
                                            process.Kill();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    loggingService.AddLog($"Failed to terminate target: {ex.Message}");
                                }
                            }
                        }

                        loggingService.AddLog("Exiting application by F4.");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        loggingService.AddLog($"F4 shutdown failed: {ex.Message}");
                        Environment.Exit(1);
                    }
                });
            }
        };
    }

    public void ExecuteStartupLogic()
    {
        bool autoStartedByThirdParty = false;

        // Check and repair protocol association if user has registered it
        if (configService.IsProtocolRegistrationEnabled())
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

        if (IsProtocolAutoStart)
        {
            loggingService.AddLog($"[Startup] Protocol launch detected. Starting monitoring.");
            autoStartedByThirdParty = true;

            // Always launch the associated program
            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
                launchService.Launch(path);

            // Start monitoring with countdown only if the user has opted in
            if (configService.IsAutoStartFromThirdPartyEnabled() &&
                configService.IsAutoStartMonitoringOnProtocolLaunchEnabled())
            {
                var targetProc = configService.GetDefaultProcessName();
                if (!string.IsNullOrWhiteSpace(targetProc) && !stateManager.IsRunning)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await stateManager.StartAsync(targetProc, programAlreadyLaunched: true);
                        }
                        catch (Exception ex)
                        {
                            loggingService.AddLog($"Startup monitoring failed: {ex.Message}");
                        }
                    });
                }
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
            DialogKey.SelectApplication,
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
                resolvedPath = path;
            }
            configService.SetAssociatedLaunchPath(resolvedPath);
        }
    }
}
