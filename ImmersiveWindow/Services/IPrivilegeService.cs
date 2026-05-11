namespace ImmersiveWindow.Services;

public interface IPrivilegeService
{
    bool IsAdministrator();
    void RestartAsAdministrator();
}