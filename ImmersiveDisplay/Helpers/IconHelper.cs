using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Helpers;

public static class IconHelper
{
    private static string IconsDir => Path.Combine(AppContext.BaseDirectory, "icons");

    public static IconImportResult? SelectAndCopyIcon()
    {
        var path = NativeDialogHelper.ShowOpenFileDialog(
            "Select Icon File",
            "Icon Files|*.ico|All files (*.*)|*.*");

        if (path == null) return null;

        return CopyIconToDir(path);
    }

    public static IconImportResult? SaveIconFromBase64(string fileName, string base64Data)
    {
        try
        {
            Directory.CreateDirectory(IconsDir);

            byte[] bytes = Convert.FromBase64String(base64Data);
            string resolvedName = ResolveConflict(IconsDir, fileName, bytes.Length);
            string destPath = Path.Combine(IconsDir, resolvedName);

            File.WriteAllBytes(destPath, bytes);

            string base64 = GetIconBase64(resolvedName);

            return new IconImportResult
            {
                FileName = resolvedName,
                Base64 = base64,
                ConflictResolved = resolvedName != fileName,
                ResolvedFileName = resolvedName != fileName ? resolvedName : null
            };
        }
        catch
        {
            return null;
        }
    }

    public static string GetIconBase64(string fileName)
    {
        try
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string fullPath = Path.Combine(IconsDir, fileName);
            if (!File.Exists(fullPath)) return "";

            byte[] bytes = File.ReadAllBytes(fullPath);
            string base64String = Convert.ToBase64String(bytes);
            return $"data:image/x-icon;base64,{base64String}";
        }
        catch
        {
            return "";
        }
    }

    private static IconImportResult? CopyIconToDir(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(IconsDir);

            string fileName = Path.GetFileName(sourcePath);
            long fileSize = new FileInfo(sourcePath).Length;
            string resolvedName = ResolveConflict(IconsDir, fileName, fileSize);
            string destPath = Path.Combine(IconsDir, resolvedName);

            File.Copy(sourcePath, destPath, true);

            string base64 = GetIconBase64(resolvedName);

            return new IconImportResult
            {
                FileName = resolvedName,
                Base64 = base64,
                ConflictResolved = resolvedName != fileName,
                ResolvedFileName = resolvedName != fileName ? resolvedName : null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveConflict(string destDir, string desiredFileName, long newFileSize)
    {
        string targetPath = Path.Combine(destDir, desiredFileName);
        if (!File.Exists(targetPath))
            return desiredFileName;

        long existingSize = new FileInfo(targetPath).Length;
        if (existingSize == newFileSize)
            return desiredFileName; // Same size, overwrite

        // Different size, auto-rename
        string name = Path.GetFileNameWithoutExtension(desiredFileName);
        string ext = Path.GetExtension(desiredFileName);
        int counter = 1;
        do
        {
            targetPath = Path.Combine(destDir, $"{name}_{counter}{ext}");
            counter++;
        }
        while (File.Exists(targetPath));

        return Path.GetFileName(targetPath);
    }
}
