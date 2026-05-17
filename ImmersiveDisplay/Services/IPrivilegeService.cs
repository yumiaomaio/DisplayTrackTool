namespace ImmersiveDisplay.Services;

public interface IPrivilegeService
{
    bool IsAdministrator();
    void RestartAsAdministrator();
}