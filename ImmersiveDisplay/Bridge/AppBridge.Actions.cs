using System.Text.Json;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Models;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Bridge;

public partial class AppBridge
{
    public bool IsRunning => stateManager.IsRunning;
    public int WaitingCountdown => stateManager.WaitingCountdown;

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
                NativeDialogHelper.ShowError(DialogKey.StartMonitoringError, ex.Message);
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
        if (mode.Equals("color", StringComparison.OrdinalIgnoreCase)) targetMode = BackgroundMode.COLOR;
        else if (mode.Equals("image", StringComparison.OrdinalIgnoreCase)) targetMode = BackgroundMode.IMAGE;
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
            DialogKey.SelectBackgroundImage,
            "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*");

        if (path == null) return;

        string? fileName = OverlayImageHelper.CopyToBackgrounds(path);
        if (fileName != null)
        {
            configService.SetBackgroundMode(BackgroundMode.IMAGE);
            configService.SetBackgroundImageFileName(fileName);
        }
        else
        {
            NativeDialogHelper.ShowError(DialogKey.CopyImageFailed);
        }
    }

    public void SelectAssociatedProgram()
    {
        var path = NativeDialogHelper.ShowOpenFileDialog(
            DialogKey.SelectApplication,
            "Applications & Shortcuts|*.exe;*.lnk;*.url|All files (*.*)|*.*");

        if (path == null) return;

        string resolvedPath = path switch
        {
            _ when path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) => ShortcutResolver.Resolve(path),
            _ when path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) => ShortcutResolver.ResolveUrl(path),
            _ => path
        };
        configService.SetAssociatedLaunchPath(resolvedPath);
    }

    public void ShowAbout()
    {
        NativeDialogHelper.ShowInfo(DialogKey.AboutMessage, DialogKey.AboutTitle);
    }

    public string GetProcessCommandLine(string processName)
    {
        string? commandLine = ProcessHelper.GetProcessCommandLine(processName, out bool permissionDenied);

        if (permissionDenied)
        {
            loggingService.AddLog($"[AppBridge] Command line detection failed (Permission Denied) for '{processName}'.");
            NativeDialogHelper.ShowWarning(DialogKey.CommandLinePermission, DialogKey.CommandLinePermissionTitle);
        }

        return commandLine ?? "";
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
}
