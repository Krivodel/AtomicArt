using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public interface IImageGenerationApiClient
{
    Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        Guid logicalGenerationId,
        int attemptNumber,
        string providerCredential,
        CancellationToken ct = default);
}
