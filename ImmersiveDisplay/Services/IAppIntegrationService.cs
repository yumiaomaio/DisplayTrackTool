namespace ImmersiveDisplay.Services;

public interface IAppIntegrationService
{
    void InitializeHooksAndTriggers();
    void ExecuteStartupLogic();
    void SelectAssociatedProgram();
}

