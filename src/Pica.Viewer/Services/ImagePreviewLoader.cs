using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using SkiaSharp;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ImagePreviewLoader
{
    internal const int PreviewDecodeWidth = 128;

    private readonly ILogger<ImagePreviewLoader> _logger;

    public ImagePreviewLoader(ILogger<ImagePreviewLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DecodedImagePreview> LoadAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        return await Task.Run(() => Load(item, ct), ct).ConfigureAwait(false);
    }

    private static DecodedImagePreview DecodePreviewFile(
        string previewPath,
        PixelSize sourcePixelSize,
        CancellationToken ct)
    {
        Bitmap bitmap = AvaloniaBitmapDecoder.DecodeFile(previewPath, ct);

        return new DecodedImagePreview(bitmap, sourcePixelSize);
    }

    private static DecodedImagePreview DecodeSourcePreview(
        Stream sourceStream,
        PixelSize sourcePixelSize,
        CancellationToken ct)
    {
        Bitmap bitmap = AvaloniaBitmapDecoder.Decode(
            () => Bitmap.DecodeToWidth(
                sourceStream,
                PreviewDecodeWidth,
                BitmapInterpolationMode.MediumQuality),
            ct);

        return new DecodedImagePreview(bitmap, sourcePixelSize);
    }

    private static PixelSize ReadSourcePixelSize(Stream sourceStream)
    {
        using SKManagedStream managedStream = new(sourceStream);
        using SKCodec codec = SKCodec.Create(managedStream)
            ?? throw new InvalidDataException("Failed to read the image dimensions.");
        SKImageInfo imageInfo = codec.Info;
        bool swapDimensions = codec.EncodedOrigin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        return swapDimensions
            ? new PixelSize(imageInfo.Height, imageInfo.Width)
            : new PixelSize(imageInfo.Width, imageInfo.Height);
    }

    private static string? GetExistingPreviewPath(PicaImageItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PreviewFilePath))
        {
            return null;
        }

        string previewPath = Path.GetFullPath(item.PreviewFilePath);

        return File.Exists(previewPath) ? previewPath : null;
    }

    private DecodedImagePreview Load(PicaImageItem item, CancellationToken ct)
    {
        string sourcePath = Path.GetFullPath(item.FilePath);
        using FileStream sourceStream = File.OpenRead(sourcePath);
        PixelSize sourcePixelSize = ReadSourcePixelSize(sourceStream);
        sourceStream.Position = 0;
        string? existingPreviewPath = GetExistingPreviewPath(item);

        if (existingPreviewPath is not null)
        {
            try
            {
                return DecodePreviewFile(existingPreviewPath, sourcePixelSize, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to use the prebuilt image thumbnail.");
            }
        }

        return DecodeSourcePreview(sourceStream, sourcePixelSize, ct);
    }
}
