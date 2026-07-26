namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal interface IGalleryPreviewBitmapProvider
{
    Task<GalleryPreviewBitmapLease?> AcquireAsync(
        string imagePath,
        CancellationToken ct);
}
