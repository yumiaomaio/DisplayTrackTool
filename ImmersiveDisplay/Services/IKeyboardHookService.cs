namespace ImmersiveDisplay.Services;

public interface IKeyboardHookService
{
    event Action<int>? KeyPressed;
    void Install();
    void Uninstall();
}
