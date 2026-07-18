using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiter
{
    public static int MaximumConcurrency => Environment.ProcessorCount;

    private readonly SemaphoreConcurrencyGate _gate = new(MaximumConcurrency);

    public Task WaitAsync(CancellationToken ct)
    {
        return _gate.WaitAsync(ct);
    }

    public void Release()
    {
        _gate.Release();
    }
}
