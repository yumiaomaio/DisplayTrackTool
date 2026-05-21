using System.Threading;

namespace ImmersiveDisplay.Helpers;

internal class UiSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        UiDispatcher.BeginInvoke(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        // For simple message-based dispatcher, we use BeginInvoke for send as well
        // to avoid deadlocks, or just implement a synchronous marshal if needed.
        UiDispatcher.BeginInvoke(() => d(state));
    }

    public override SynchronizationContext CreateCopy() => new UiSynchronizationContext();
}
