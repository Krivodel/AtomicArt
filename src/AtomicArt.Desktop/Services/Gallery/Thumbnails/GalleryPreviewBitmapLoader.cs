using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewBitmapLoader : IGalleryPreviewBitmapLoader
{
    private readonly ILogger<GalleryPreviewBitmapLoader> _logger;
    private readonly int _thumbnailShortSidePixels;
    private readonly SemaphoreSlim _decodeSemaphore;

    public GalleryPreviewBitmapLoader(
        ILogger<GalleryPreviewBitmapLoader> logger,
        GalleryThumbnailSpecification specification,
        IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _thumbnailShortSidePixels = specification.ShortSidePixels;
        _decodeSemaphore = new SemaphoreSlim(
            options.Value.MaximumPreviewDecodeConcurrency,
            options.Value.MaximumPreviewDecodeConcurrency);
    }

    public async Task<Bitmap?> LoadAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        await _decodeSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await Task
                .Run(() => Decode(imagePath, ct), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Failed to load a gallery preview bitmap.");

            return null;
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    private Bitmap Decode(string imagePath, CancellationToken ct)
    {
        using FileStream stream = File.OpenRead(imagePath);
        return PreviewBitmapDecoder.Decode(
            stream,
            _thumbnailShortSidePixels,
            ct);
    }
}
