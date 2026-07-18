namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationActivityTracker
{
    bool IsActive { get; }

    event EventHandler? ActivityChanged;

    void Start(Guid correlationId, GenerationActivityPhase phase);

    void Complete(Guid correlationId, GenerationActivityPhase phase);

    Task WaitUntilIdleAsync(CancellationToken ct);
}
