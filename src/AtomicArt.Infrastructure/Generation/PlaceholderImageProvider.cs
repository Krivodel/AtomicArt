namespace AtomicArt.Infrastructure.Generation;

internal abstract class PlaceholderImageProvider : IPlaceholderImageProvider
{
    public async Task<PlaceholderImage> GetNextAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        ct.ThrowIfCancellationRequested();

        return await GetNextCoreAsync(modelId, itemIndex, ct).ConfigureAwait(false);
    }

    protected abstract Task<PlaceholderImage> GetNextCoreAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct);
}
