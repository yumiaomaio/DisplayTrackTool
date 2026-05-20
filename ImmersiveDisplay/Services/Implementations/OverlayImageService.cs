using System.IO;


namespace ImmersiveDisplay.Services.Implementations;

public class OverlayImageService(
    IConfigService configService,
    IDialogService dialogService,
    ILoggingService loggingService)
    : IOverlayImageService
{
    public string GetImageBase64(string fileName)
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
        catch (Exception ex)
        {
            loggingService.AddLog($"[OverlayImageService] Error encoding image: {ex.Message}");
            return "";
        }
    }

    public void SelectAndSetBackgroundImage()
    {
        ImmersiveDisplay.Helpers.UiDispatcher.BeginInvoke(() => 
        {
            var path = dialogService.ShowOpenFileDialog(
                "Select a Background Image",
                "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*");

            if (path != null)
            {
                string fileName = Path.GetFileName(path);
                string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
                Directory.CreateDirectory(backgroundsDir);
                string destPath = Path.Combine(backgroundsDir, fileName);
                try
                {
                    File.Copy(path, destPath, true);
                    configService.SetBackgroundMode(Models.BackgroundMode.IMAGE);
                    configService.SetBackgroundImageFileName(fileName);
                }
                catch (Exception ex)
                {
                    dialogService.ShowError($"Error copying file: {ex.Message}");
                }
            }
        });
    }
}
