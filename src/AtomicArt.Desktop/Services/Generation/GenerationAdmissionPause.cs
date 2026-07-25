namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationAdmissionPause : IDisposable
{
    private readonly Action _release;
    private int _isDisposed;

    internal GenerationAdmissionPause(Action release)
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
