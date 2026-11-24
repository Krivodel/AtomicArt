using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewBitmapProvider : IGalleryPreviewBitmapProvider, IDisposable
{
    private readonly PreviewBitmapCache<string, GalleryPreviewBitmapProvider> _cache;

    public GalleryPreviewBitmapProvider(
        IGalleryPreviewBitmapLoader loader,
        ILogger<GalleryPreviewBitmapProvider> logger,
        IOptions<GalleryOptions> options)
        : this(loader, logger, GetMaximumCacheSizeBytes(options))
    {
    }

    internal GalleryPreviewBitmapProvider(
        IGalleryPreviewBitmapLoader loader,
        ILogger<GalleryPreviewBitmapProvider> logger,
        long maximumCacheSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _cache = new PreviewBitmapCache<string, GalleryPreviewBitmapProvider>(
            loader.LoadAsync, logger, maximumCacheSizeBytes, StringComparer.OrdinalIgnoreCase);
    }

    public Task<GalleryPreviewBitmapLease?> AcquireAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        return _cache.AcquireAsync(imagePath, ct);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    private static long GetMaximumCacheSizeBytes(IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Value.MaximumPreviewCacheSizeBytes;
    }
}
