namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryImageViewerItem(
    Guid Id,
    GalleryImageViewerSource Source,
    Func<CancellationToken, Task>? ToggleFavoriteAsync = null,
    Func<bool>? GetIsFavorite = null);
