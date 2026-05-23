// File: Helpers/OverlayImageHelper.cs

namespace ImmersiveDisplay.Helpers;

public static class OverlayImageHelper
{
    public static string GetImageBase64(string fileName)
    {
        try
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
            string fullPath = Path.Combine(backgroundsDir, fileName);
            if (!File.Exists(fullPath)) return "";

            byte[] imageBytes = File.ReadAllBytes(fullPath);
            string base64String = Convert.ToBase64String(imageBytes);
            string extension = Path.GetExtension(fullPath).ToLower().TrimStart('.');
            string mimeType = extension == "png" ? "image/png" : "image/jpeg";
            return $"data:{mimeType};base64,{base64String}";
        }
        catch
        {
            return "";
        }
    }

    public static string? CopyToBackgrounds(string sourcePath)
    {
        try
        {
            string fileName = Path.GetFileName(sourcePath);
            string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
            Directory.CreateDirectory(backgroundsDir);
            string destPath = Path.Combine(backgroundsDir, fileName);
            File.Copy(sourcePath, destPath, true);
            return fileName;
        }
        catch
        {
            return null;
        }
    }
}
