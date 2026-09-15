using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryBitmapImageViewerSource(
    string ModelId,
    string FileName,
    IPicaImageBitmapSource BitmapSource,
    string? FilePath = null) : GalleryImageViewerSource;
