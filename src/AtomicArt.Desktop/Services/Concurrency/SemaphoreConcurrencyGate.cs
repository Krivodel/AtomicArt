namespace AtomicArt.Desktop.Services.Concurrency;

internal sealed class SemaphoreConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore;

    public SemaphoreConcurrencyGate(int maximumConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrency, 1);

        _semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public Task WaitAsync(CancellationToken ct)
    {
        return _semaphore.WaitAsync(ct);
    }

    public void Release()
    {
        _semaphore.Release();
    }
}
