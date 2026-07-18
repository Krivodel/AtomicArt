using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public interface IImageGenerationApiClient
{
    Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default);
}
