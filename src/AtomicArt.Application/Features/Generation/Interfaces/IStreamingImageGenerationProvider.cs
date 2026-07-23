using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IStreamingImageGenerationProvider
{
    Task<IProviderGenerationStream> CreateStreamAsync(
        StreamingGenerationProviderContext context,
        CancellationToken ct);
}
