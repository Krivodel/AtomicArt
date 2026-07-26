using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal interface IGalleryPreviewBitmapLoader
{
    Task<Bitmap?> LoadAsync(string imagePath, CancellationToken ct);
}
