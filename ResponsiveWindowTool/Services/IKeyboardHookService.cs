// File: Services/IKeyboardHookService.cs
using System;

namespace ResponsiveWindowTool.Services
{
    public interface IKeyboardHookService
    {
        event Action<int> KeyPressed;
        void Start();
        void Stop();
    }
}