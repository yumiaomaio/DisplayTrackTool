using System.IO;

namespace ImmersiveDisplay.Interop;

public static class ShortcutResolver
{
    public static Action<string>? LogAction { get; set; }

    /// <summary>
    /// Resolves a .lnk shortcut file using the Windows Shell COM Automation object.
    /// 
    /// This method is highly stable and leverages explorer.exe's internal logic.
    /// </summary>
    public static string Resolve(string lnkPath)
    {
        if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath))
            return lnkPath;

        LogAction?.Invoke($"> Shell.Application: Analyzing {Path.GetFileName(lnkPath)}");

        try
        {
            // 1. Initialize the Shell object (Late Binding)
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return lnkPath;

            dynamic shell = Activator.CreateInstance(shellType)!;

            // 2. Get the folder and the item
            string? directory = Path.GetDirectoryName(lnkPath);
            string file = Path.GetFileName(lnkPath);

            if (directory == null) return lnkPath;

            var folder = shell.NameSpace(directory);
            var folderItem = folder.ParseName(file);

            if (folderItem == null)
            {
                LogAction?.Invoke($"> ERROR: Shell could not parse link file.");
                return lnkPath;
            }

            // 3. Get the link object
            if (!folderItem.IsLink)
            {
                LogAction?.Invoke($"> Info: Target is not a shortcut.");
                return lnkPath;
            }

            dynamic link = folderItem.GetLink;
            string target = link.Path;
            string args = link.Arguments;

            LogAction?.Invoke($"> Shell Target: {target}");
            if (!string.IsNullOrWhiteSpace(args))
                LogAction?.Invoke($"> Shell Args: {args}");

            if (string.IsNullOrWhiteSpace(args))
                return target;

            // Handle quoting if needed
            if (target.Contains(' ') && !target.StartsWith("\""))
                target = $"\"{target}\"";

            return $"{target} {args}";
        }
        catch (Exception ex)
        {
            LogAction?.Invoke($"> Shell Error: {ex.Message}");
            return lnkPath;
        }
    }
}
