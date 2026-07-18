namespace AtomicArt.Desktop.Tests.Services;

internal sealed class TestSubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private bool _isDisposed;

    public TestSubscription(Action unsubscribe)
    {
        ArgumentNullException.ThrowIfNull(unsubscribe);

        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _unsubscribe();
    }
}
