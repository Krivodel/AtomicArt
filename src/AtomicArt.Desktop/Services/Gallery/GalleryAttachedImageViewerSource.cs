using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryAttachedImageViewerSource(
    AttachedImageDto Image) : GalleryImageViewerSource;
