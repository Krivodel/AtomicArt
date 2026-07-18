namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationResultStorage
{
    string? GetExpectedResultPathOrDefault(
        Guid batchId,
        Guid itemId,
        string contentType);

    Task SaveAsync(
        Guid batchId,
        Guid itemId,
        GenerationImageContentValidationResult content,
        CancellationToken ct);
}
