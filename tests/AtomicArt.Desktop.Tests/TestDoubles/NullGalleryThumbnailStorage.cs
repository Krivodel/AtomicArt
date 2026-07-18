using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class NullGalleryThumbnailStorage : IGalleryThumbnailStorage
{
    public string? GetThumbnailPathOrDefault(
        Guid batchId,
        Guid itemId,
        string modelId)
    {
        return null;
    }

    public Task SaveAsync(
        Guid batchId,
        Guid itemId,
        string modelId,
        string? fullImagePath,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
