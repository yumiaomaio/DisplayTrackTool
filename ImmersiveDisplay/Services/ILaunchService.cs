namespace ImmersiveDisplay.Services;

public interface ILaunchService
{
    void Launch(string commandLine);
    void ClearHistory();
}
