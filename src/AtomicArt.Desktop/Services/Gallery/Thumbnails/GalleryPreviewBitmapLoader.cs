using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Media.Imaging;

using SkiaSharp;

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
        ct.ThrowIfCancellationRequested();
        using FileStream stream = File.OpenRead(imagePath);
        PixelSize pixelSize = ReadPixelSize(stream, ct);
        PixelSize decodeSize = GalleryThumbnailSizeCalculator.Calculate(
            pixelSize.Width,
            pixelSize.Height);
        stream.Position = 0;
        Bitmap bitmap = decodeSize != pixelSize
            ? DecodeReducedBitmap(stream, pixelSize, decodeSize)
            : new Bitmap(stream);

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            ct.ThrowIfCancellationRequested();
        }

        return bitmap;
    }

    private static Bitmap DecodeReducedBitmap(
        Stream stream,
        PixelSize pixelSize,
        PixelSize decodeSize)
    {
        if (pixelSize.Width >= pixelSize.Height)
        {
            return Bitmap.DecodeToHeight(
                stream,
                decodeSize.Height,
                BitmapInterpolationMode.MediumQuality);
        }

        return Bitmap.DecodeToWidth(
            stream,
            decodeSize.Width,
            BitmapInterpolationMode.MediumQuality);
    }

    private static PixelSize ReadPixelSize(Stream stream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using SKManagedStream managedStream = new(stream);
        using SKCodec codec = SKCodec.Create(managedStream)
            ?? throw new InvalidDataException("Gallery preview image dimensions could not be read.");
        SKImageInfo imageInfo = codec.Info;
        bool swapDimensions = codec.EncodedOrigin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
        ct.ThrowIfCancellationRequested();

        return swapDimensions
            ? new PixelSize(imageInfo.Height, imageInfo.Width)
            : new PixelSize(imageInfo.Width, imageInfo.Height);
    }
}
