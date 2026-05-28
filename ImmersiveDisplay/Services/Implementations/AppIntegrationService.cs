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
    private bool _isProtocolAutoStart;

    public bool ShouldShowUacPrompt
    {
        get
        {
            if (PrivilegeHelper.IsAdministrator()) return false;
            if (!_isProtocolAutoStart) return true;
            if (configService.IsAutoStartFromThirdPartyEnabled() &&
                configService.IsAutoStartMonitoringOnProtocolLaunchEnabled() &&
                IsAssociatedPathExe())
                return false;
            return true;
        }
    }

    public void Initialize(bool isProtocolAutoStart)
    {
        _isProtocolAutoStart = isProtocolAutoStart;

        ShortcutResolver.LogAction = (msg) => loggingService.AddLog(msg);
        keyboardHook.KeyPressed += OnKeyPressed;

        // --- Startup Logic ---
        bool autoStartedByThirdParty = false;

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

        if (_isProtocolAutoStart && configService.IsAutoStartFromThirdPartyEnabled())
        {
            loggingService.AddLog("[Startup] Protocol launch detected. Starting associated program.");
            autoStartedByThirdParty = true;

            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
                launchService.Launch(path);

            if (configService.IsAutoStartMonitoringOnProtocolLaunchEnabled())
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

        if (!autoStartedByThirdParty && configService.IsLaunchOnAppStartupEnabled())
        {
            var path = configService.GetAssociatedLaunchPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                launchService.Launch(path);
            }
        }
    }

    private void OnKeyPressed(int vkCode)
    {
        const int vkF9 = 0x78;
        const int vkF12 = 0x7B;
        const int vkF4 = 0x73;

        if (vkCode == vkF9) HandleF9();
        else if (vkCode == vkF12) HandleF12();
        else if (vkCode == vkF4) HandleF4();
    }

    private void HandleF9()
    {
        var processName = configService.GetDefaultProcessName();
        if (stateManager.IsRunning || string.IsNullOrWhiteSpace(processName)) return;

        loggingService.AddLog("F9 key pressed. Starting...");
        _ = Task.Run(async () =>
        {
            try { await stateManager.StartAsync(processName); }
            catch (Exception ex) { loggingService.AddLog($"Failed to start from F9 hook: {ex.Message}"); }
        });
    }

    private void HandleF12()
    {
        if (!stateManager.IsRunning) return;

        loggingService.AddLog("F12 key pressed. Shutting down...");
        _ = Task.Run(async () =>
        {
            try
            {
                await stateManager.StopAsync();
                launchService.ClearHistory();
            }
            catch (Exception ex) { loggingService.AddLog($"Failed to stop from F12 hook: {ex.Message}"); }
        });
    }

    private void HandleF4()
    {
        if (!stateManager.IsRunning || !_isProtocolAutoStart) return;

        loggingService.AddLog("F4 key pressed. Terminating target and exiting...");
        var hwnd = stateManager.CurrentTargetHwnd;
        _ = Task.Run(async () =>
        {
            try
            {
                await stateManager.StopAsync();
                KillTargetProcess(hwnd);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                loggingService.AddLog($"F4 shutdown failed: {ex.Message}");
                Environment.Exit(1);
            }
        });
    }

    private void KillTargetProcess(IntPtr? hwnd)
    {
        if (!hwnd.HasValue) return;

        NativeMethods.GetWindowThreadProcessId(hwnd.Value, out uint pid);
        if (pid == 0) return;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (process.HasExited) return;

            process.CloseMainWindow();
            if (!process.WaitForExit(3000)) process.Kill();
        }
        catch (Exception ex) { loggingService.AddLog($"Failed to terminate target: {ex.Message}"); }
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
}
