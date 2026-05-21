namespace ImmersiveDisplay.Services;

public interface IAppIntegrationService
{
    bool IsProtocolAutoStart { get; set; }
    void InitializeHooksAndTriggers();
    void ExecuteStartupLogic();
    void SelectAssociatedProgram();
}

