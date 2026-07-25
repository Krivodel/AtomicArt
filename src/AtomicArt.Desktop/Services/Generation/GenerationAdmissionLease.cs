namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationAdmissionLease : IDisposable
{
    private readonly Action _release;
    private int _isDisposed;

    internal GenerationAdmissionLease(Action release)
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
