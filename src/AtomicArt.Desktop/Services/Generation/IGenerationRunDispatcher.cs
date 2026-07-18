namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationRunDispatcher
{
    Task EnqueueAsync(GenerationRunRequest request, CancellationToken ct);
}
