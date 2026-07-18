using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageGenerationContentProvider
{
    Task<ImageGenerationContentResult> GetContentAsync(
        ImageGenerationContentProviderContext context,
        CancellationToken ct);
}
