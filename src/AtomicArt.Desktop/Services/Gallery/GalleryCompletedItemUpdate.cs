using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryCompletedItemUpdate(
    GenerationItemDto Item,
    string? TrustedImagePath,
    string? ThumbnailPath);
