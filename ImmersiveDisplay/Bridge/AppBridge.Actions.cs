using System.Text.Json;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Models;

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

    private void SelectAssociatedProgram()
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

    private void HandleAppProtocol(string uri)
    {
        try
        {
            if (string.IsNullOrEmpty(uri)) return;

            var uriObj = new Uri(uri);
            string scheme = uriObj.Scheme.ToLowerInvariant();
            string path = Uri.UnescapeDataString(uriObj.AbsolutePath).Trim();

            if (scheme == "file")
            {
                if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    loggingService.AddLog("[AppBridge] Non-executable file protocol ignored.");
                    return;
                }

                string finalPath = path;
                string exeTarget = finalPath;

                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    finalPath = ShortcutResolver.Resolve(path);
                    loggingService.AddLog($"[AppBridge] Resolved LNK to: {finalPath}");

                    exeTarget = ExtractTargetPath(finalPath);
                    if (string.IsNullOrEmpty(exeTarget) || !exeTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        loggingService.AddLog("[AppBridge] Resolved LNK does not point to a valid EXE. Discarding.");
                        return;
                    }
                }

                configService.SetAssociatedLaunchPath(finalPath);

                string processName = Path.GetFileNameWithoutExtension(exeTarget);
                if (!string.IsNullOrEmpty(processName))
                    configService.SetDefaultProcessName(processName);
            }
            else if (scheme == "app" || scheme.StartsWith("http"))
            {
                configService.SetAssociatedLaunchPath(uri);
                loggingService.AddLog($"[AppBridge] Saving URI launch path: {uri}");
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

    private bool CleanAssociation()
    {
        bool cleaned = ProtocolHelper.CleanAllAssociationUrls();
        configService.SetAutoStartFromThirdParty(false);
        configService.SetProtocolRegistrationEnabled(false);
        return cleaned;
    }

    private bool CreateShareShortcut()
    {
        var path = configService.GetAssociatedLaunchPath();
        if (string.IsNullOrWhiteSpace(path)) return false;

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string targetPath = ExtractTargetPath(path);
        string fileName = Path.GetFileNameWithoutExtension(targetPath);
        string shortcutPath = Path.Combine(desktop, $"{fileName}.lnk");
        return ShortcutResolver.CreateLnk(shortcutPath, path);
    }
    
    private void CreateAssociationUrls(string json)
    {
        var request = JsonSerializer.Deserialize(json, AppJsonContext.Default.UrlRequest);
        if (request?.Entries == null) return;
        ProtocolHelper.CreateMultipleUrlShortcuts(request.Entries, request.IconFileName);
    }

    private static string ExtractTargetPath(string commandLine)
    {
        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith("\""))
        {
            int nextQuote = trimmed.IndexOf("\"", 1);
            return nextQuote != -1 ? trimmed.Substring(1, nextQuote - 1) : trimmed.Trim('\"');
        }
        
        if (File.Exists(trimmed)) return trimmed;
        
        if (trimmed.Contains(' '))
        {
            int firstSpace = trimmed.IndexOf(' ');
            return trimmed.Substring(0, firstSpace);
        }
        return trimmed;
    }
}
