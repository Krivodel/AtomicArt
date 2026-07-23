namespace AtomicArt.Infrastructure.Generation;

internal interface IStreamingPlaceholderImageProvider
{
    Task<StreamingPlaceholderImage> OpenNextAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct);
}
