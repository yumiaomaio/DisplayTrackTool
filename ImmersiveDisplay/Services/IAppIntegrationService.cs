namespace ImmersiveDisplay.Services;

public interface IAppIntegrationService
{
    bool IsProtocolAutoStart { get; set; }
    bool ShouldShowUacPrompt { get; }
    void InitializeHooksAndTriggers();
    void ExecuteStartupLogic();
    void SelectAssociatedProgram();
}

