// File: Services/IKeyboardHookService.cs
namespace ResponsiveWindowTool.Services;

public interface IKeyboardHookService
{
    event Action<int> KeyPressed;
    void Start();
    void Stop();
}