using System.Reflection;
using System.Threading;

namespace ImmersiveDisplay.Helpers;

internal class UiSynchronizationContext : SynchronizationContext
{
    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        UiDispatcher.BeginInvoke(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        // If already on the UI thread, execute synchronously inline to avoid deadlocks
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            d(state);
            return;
        }

        using var finishedEvent = new ManualResetEventSlim(false);
        Exception? exception = null;

        UiDispatcher.BeginInvoke(() =>
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                finishedEvent.Set();
            }
        });

        finishedEvent.Wait(); // Block the calling thread until the UI thread completes the work

        if (exception != null)
        {
            throw new TargetInvocationException("Exception occurred during synchronous dispatch.", exception);
        }
    }

    public override SynchronizationContext CreateCopy() => new UiSynchronizationContext();
}
