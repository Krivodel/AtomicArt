using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

internal sealed class SuccessfulImageGenerationApiClient : IImageGenerationApiClient
{
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);

    public Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        List<GenerationItemDto> items = Enumerable
            .Range(0, request.GenerationCount)
            .Select(index => new GenerationItemDto(
                Guid.NewGuid(),
                request.ModelId,
                "Nano Banana 2",
                request.Prompt,
                request.AspectRatio,
                request.Resolution,
                CreatedAtUtc.AddSeconds(index),
                GenerationItemStatus.Generated,
                null,
                null))
            .ToList();
        GenerationBatchDto batch = new(Guid.NewGuid(), items);

        return Task.FromResult(batch);
    }
}
