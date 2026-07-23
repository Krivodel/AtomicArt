namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationCancellationService
{
    void Register(Guid logicalGenerationId, Action cancel);

    void Unregister(Guid logicalGenerationId);

    void Cancel(Guid logicalGenerationId);
}
