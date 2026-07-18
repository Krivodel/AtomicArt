namespace AtomicArt.Infrastructure.Generation;

internal interface IPlaceholderImageProvider
{
    Task<PlaceholderImage> GetNextAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct);
}
