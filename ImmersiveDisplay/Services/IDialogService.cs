namespace ImmersiveDisplay.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title = "Info");
    void ShowWarning(string message, string title = "Warning");
    void ShowError(string message, string title = "Error");
    string? ShowOpenFileDialog(string title, string filter);
}
