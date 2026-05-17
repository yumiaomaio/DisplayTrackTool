namespace ImmersiveDisplay.Services;

public interface ITaskbarService
{
    void CaptureOriginalState();
    bool IsAutoHideEnabled();
    void SetAutoHide(bool enable);
    void RestoreOriginalState();
}
