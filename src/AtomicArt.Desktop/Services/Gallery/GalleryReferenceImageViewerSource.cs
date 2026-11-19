using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryReferenceImageViewerSource(
    GenerationReference Reference) : GalleryImageViewerSource;
