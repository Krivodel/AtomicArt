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
            .Select(index => GenerationItemDtoTestFactory.Create(
                id: Guid.NewGuid(),
                modelId: request.ModelId,
                prompt: request.Prompt,
                aspectRatio: request.AspectRatio,
                resolution: request.Resolution,
                createdAtUtc: CreatedAtUtc.AddSeconds(index)))
            .ToList();
        GenerationBatchDto batch = new(Guid.NewGuid(), items);

        return Task.FromResult(batch);
    }
}
