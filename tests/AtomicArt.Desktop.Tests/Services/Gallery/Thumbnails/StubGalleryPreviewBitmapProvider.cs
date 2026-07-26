using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

internal sealed class StubGalleryPreviewBitmapProvider : IGalleryPreviewBitmapProvider
{
    private readonly Func<string, CancellationToken, Task<GalleryPreviewBitmapLease?>>
        _acquireAsync;

    public StubGalleryPreviewBitmapProvider(
        Func<string, CancellationToken, Task<GalleryPreviewBitmapLease?>> acquireAsync)
    {
        _acquireAsync = acquireAsync
            ?? throw new ArgumentNullException(nameof(acquireAsync));
    }

    public Task<GalleryPreviewBitmapLease?> AcquireAsync(
        string imagePath,
        CancellationToken ct)
    {
        return _acquireAsync(imagePath, ct);
    }
}
