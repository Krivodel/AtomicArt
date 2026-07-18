namespace AtomicArt.Desktop.Services;

public sealed class GenerationLifecycleSubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private int _disposed;

    public GenerationLifecycleSubscription(Action unsubscribe)
    {
        ArgumentNullException.ThrowIfNull(unsubscribe);

        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
