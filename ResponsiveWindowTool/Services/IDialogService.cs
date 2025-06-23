using System.Threading.Tasks;

namespace ResponsiveWindowTool.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationDialog(string message, int timeoutSeconds);
    }
}