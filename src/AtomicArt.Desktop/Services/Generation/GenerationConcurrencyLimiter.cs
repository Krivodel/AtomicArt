using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationConcurrencyLimiter : IGenerationConcurrencyLimiter
{
    public const int MaxConcurrentGenerations = 64;

    private readonly SemaphoreConcurrencyGate _gate = new(MaxConcurrentGenerations);

    public Task WaitAsync(CancellationToken ct)
    {
        return _gate.WaitAsync(ct);
    }

    public void Release()
    {
        _gate.Release();
    }
}
