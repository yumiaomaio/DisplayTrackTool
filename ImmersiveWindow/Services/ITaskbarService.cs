namespace ImmersiveWindow.Services;

public interface ITaskbarService
{
    bool IsAutoHideEnabled();
    void SetAutoHide(bool enable);
}