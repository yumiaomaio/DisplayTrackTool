namespace ImmersiveDisplay.Services;

public interface IAppIntegrationService
{
    void Initialize(bool isProtocolAutoStart);
    bool ShouldShowUacPrompt { get; }
}
