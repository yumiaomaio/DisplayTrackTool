using ResponsiveWindowTool.Views;

namespace ResponsiveWindowTool.Services.Implementations;

public class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationDialog(string message, int timeoutSeconds)
    {
        var dialog = new ConfirmationDialog(message, System.TimeSpan.FromSeconds(timeoutSeconds));
        // Show() 是非阻塞的，我们需要一种方法来等待它的结果
        dialog.Show();
        return await dialog.GetResultAsync();
    }
}