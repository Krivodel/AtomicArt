using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

internal sealed class ThrowingImageGenerationApiClient : IImageGenerationApiClient
{
    public Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        throw new HttpRequestException("Unavailable");
    }
}
