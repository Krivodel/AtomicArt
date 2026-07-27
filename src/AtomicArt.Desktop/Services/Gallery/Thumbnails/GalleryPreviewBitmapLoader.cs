using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewBitmapLoader : IGalleryPreviewBitmapLoader
{
    private const int MaximumConcurrentDecodes = 4;

    private readonly ILogger<GalleryPreviewBitmapLoader> _logger;
    private readonly SemaphoreSlim _decodeSemaphore =
        new(MaximumConcurrentDecodes, MaximumConcurrentDecodes);

    public GalleryPreviewBitmapLoader(ILogger<GalleryPreviewBitmapLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    private static Bitmap Decode(string imagePath, CancellationToken ct)
    {
        using FileStream stream = File.OpenRead(imagePath);
        return PreviewBitmapDecoder.Decode(
            stream,
            GalleryThumbnailSpecification.ShortSidePixels,
            ct);
    }
}
