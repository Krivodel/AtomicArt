namespace AtomicArt.Desktop.Services.Gallery.Deletion;

public sealed record GalleryItemDeletionRequest(
    Guid ItemId,
    string ModelId,
    string? ImagePath,
    string? ThumbnailPath);
