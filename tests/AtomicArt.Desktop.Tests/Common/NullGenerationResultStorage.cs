using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests;

internal sealed class NullGenerationResultStorage : IGenerationResultStorage
{
    public string? GetExpectedResultPathOrDefault(
        Guid batchId,
        Guid itemId,
        string contentType)
    {
        return null;
    }

    public Task SaveAsync(
        Guid batchId,
        Guid itemId,
        GenerationImageContentValidationResult content,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
