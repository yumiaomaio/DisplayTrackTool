// File: Services/IKeyboardHookService.cs
namespace ImmersiveWindow.Services;

public interface IKeyboardHookService
{
    event Action<int> KeyPressed;
    void Start();
    void Stop();
}