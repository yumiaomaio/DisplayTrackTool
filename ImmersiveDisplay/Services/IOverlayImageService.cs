namespace ImmersiveDisplay.Services;

public interface IOverlayImageService
{
    string GetImageBase64(string fileName);
    void SelectAndSetBackgroundImage();
}
