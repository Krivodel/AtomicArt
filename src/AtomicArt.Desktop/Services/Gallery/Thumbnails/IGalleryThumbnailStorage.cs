namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public interface IGalleryThumbnailStorage
{
    string? GetThumbnailPathOrDefault(
        Guid batchId,
        Guid itemId,
        string modelId);

    Task SaveAsync(
        Guid batchId,
        Guid itemId,
        string modelId,
        string? fullImagePath,
        CancellationToken ct);
}
