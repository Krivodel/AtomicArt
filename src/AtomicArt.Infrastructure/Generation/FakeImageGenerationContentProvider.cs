using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class FakeImageGenerationContentProvider : IProviderImageGenerationContentProvider
{
    private readonly IPlaceholderImageProvider _placeholderImageProvider;

    public FakeImageGenerationContentProvider(IPlaceholderImageProvider placeholderImageProvider)
    {
        _placeholderImageProvider = placeholderImageProvider
            ?? throw new ArgumentNullException(nameof(placeholderImageProvider));
    }

    public string Provider => GenerationProviderIds.Test;

    public Task<ImageGenerationContentResult> GetContentAsync(
        ImageGenerationContentProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Request);
        ArgumentOutOfRangeException.ThrowIfNegative(context.ItemIndex);

        return CreateContentAsync(context.Request.ModelId, context.ItemIndex, ct);
    }

    private async Task<ImageGenerationContentResult> CreateContentAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct)
    {
        PlaceholderImage placeholderImage = await _placeholderImageProvider
            .GetNextAsync(modelId, itemIndex, ct)
            .ConfigureAwait(false);
        string base64Data = Convert.ToBase64String(placeholderImage.Content);

        return new ImageGenerationContentResult(
            placeholderImage.ContentType,
            base64Data);
    }
}
