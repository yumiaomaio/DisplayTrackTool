// File: Services/IKeyboardHookService.cs
namespace ImmersiveDisplay.Services;

public interface IKeyboardHookService
{
    event Action<int> KeyPressed;
    void Start();
    void Stop();
}