using System.Text.Json;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Bridge;

public class AppProtocolHandler(IConfigService configService, ILoggingService loggingService)
{
    public void HandleAppProtocol(string uri)
    {
        loggingService.AddLog($"[AppBridge] App Protocol trigger received: {uri}");
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
                loggingService.AddLog($"[AppBridge] Saving URI launch path: {uri}");
                configService.SetAssociatedLaunchPath(uri);
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

    public void CreateAssociationUrls(string json)
    {
        var request = JsonSerializer.Deserialize(json, AppJsonContext.Default.UrlRequest);
        if (request?.Entries == null) return;
        ProtocolHelper.CreateMultipleUrlShortcuts(request.Entries, request.IconFileName);
    }

    public bool QuickRegisterAssociation()
    {
        try
        {
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

    public bool CleanAssociation()
    {
        bool cleaned = ProtocolHelper.CleanAllAssociationUrls();
        configService.SetAutoStartFromThirdParty(false);
        configService.SetProtocolRegistrationEnabled(false);
        return cleaned;
    }

    public bool CreateShareShortcut()
    {
        var path = configService.GetAssociatedLaunchPath();
        if (string.IsNullOrWhiteSpace(path)) return false;

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string targetPath = ExtractTargetPath(path);
        string fileName = Path.GetFileNameWithoutExtension(targetPath);
        string shortcutPath = Path.Combine(desktop, $"{fileName}.lnk");
        return ShortcutResolver.CreateLnk(shortcutPath, path);
    }

    public static string ExtractTargetPath(string commandLine)
    {
        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith("\""))
        {
            int nextQuote = trimmed.IndexOf("\"", 1);
            return nextQuote != -1 ? trimmed.Substring(1, nextQuote - 1) : trimmed.Trim('\"');
        }
        if (File.Exists(trimmed))
            return trimmed;
        if (trimmed.Contains(' '))
        {
            int firstSpace = trimmed.IndexOf(' ');
            return trimmed.Substring(0, firstSpace);
        }
        return trimmed;
    }
}
