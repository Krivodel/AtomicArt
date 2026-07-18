namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationConcurrencyLimiter
{
    Task WaitAsync(CancellationToken ct);

    void Release();
}
