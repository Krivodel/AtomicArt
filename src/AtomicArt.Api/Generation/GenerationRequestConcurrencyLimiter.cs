using Microsoft.Extensions.Options;

namespace AtomicArt.Api.Generation;

public sealed class GenerationRequestConcurrencyLimiter
    : IGenerationRequestConcurrencyLimiter
{
    private readonly SemaphoreSlim _semaphore;

    public GenerationRequestConcurrencyLimiter(
        IOptions<GenerationServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _semaphore = new SemaphoreSlim(
            options.Value.MaxConcurrentGenerations,
            options.Value.MaxConcurrentGenerations);
    }

    public IDisposable? TryAcquire()
    {
        return _semaphore.Wait(0)
            ? new Releaser(_semaphore)
            : null;
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore
                ?? throw new ArgumentNullException(nameof(semaphore));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
