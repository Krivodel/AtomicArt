namespace AtomicArt.Desktop.Services.Concurrency;

public abstract class SemaphoreConcurrencyLimiter
{
    private readonly SemaphoreSlim _semaphore;

    protected SemaphoreConcurrencyLimiter(int maximumConcurrency)
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
