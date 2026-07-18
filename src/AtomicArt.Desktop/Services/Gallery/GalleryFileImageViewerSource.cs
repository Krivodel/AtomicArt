namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryFileImageViewerSource(
    string ModelId,
    string ImagePath,
    string? ThumbnailPath = null) : GalleryImageViewerSource;
