namespace AtomicArt.Desktop.Services.Paths;

public sealed class DataRootAccessLease : IDisposable
{
    private readonly Action _release;
    private int _isDisposed;

    internal DataRootAccessLease(Action release)
    {
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _release();
        }
    }
}
