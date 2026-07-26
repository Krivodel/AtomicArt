using Avalonia;

using SkiaSharp;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailGenerator : IGalleryThumbnailGenerator
{
    private const string UnsupportedSourceImageMessage =
        "Thumbnail source image format is not supported.";

    private readonly GalleryThumbnailImageFormat _thumbnailImageFormat;

    public GalleryThumbnailGenerator(GalleryThumbnailImageFormat thumbnailImageFormat)
    {
        ArgumentNullException.ThrowIfNull(thumbnailImageFormat);

        _thumbnailImageFormat = thumbnailImageFormat;
    }

    public async Task<byte[]> CreateThumbnailAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        EnsureSourceImageSizeIsAllowed(imagePath);
        byte[] sourceBytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        using SKBitmap sourceBitmap = DecodeSourceBitmap(sourceBytes);
        PixelSize thumbnailSize = GalleryThumbnailSizeCalculator.Calculate(
            sourceBitmap.Width,
            sourceBitmap.Height);
        using SKBitmap thumbnailBitmap = CreateThumbnailBitmap(sourceBitmap, thumbnailSize);
        using SKImage image = SKImage.FromBitmap(thumbnailBitmap);
        using SKData encodedImage = image.Encode(
            _thumbnailImageFormat.EncodedFormat,
            _thumbnailImageFormat.EncodingQuality)
            ?? throw new InvalidOperationException("Thumbnail image could not be encoded.");

        return encodedImage.ToArray();
    }

    private static void EnsureSourceImageSizeIsAllowed(string imagePath)
    {
        FileInfo fileInfo = new(imagePath);

        if (fileInfo.Length > GalleryThumbnailSpecification.MaxSourceImageBytes)
        {
            throw new InvalidDataException("Thumbnail source image exceeds the 500 MB size limit.");
        }
    }

    private static SKBitmap DecodeSourceBitmap(byte[] sourceBytes)
    {
        try
        {
            return SKBitmap.Decode(sourceBytes)
                ?? throw new InvalidDataException(UnsupportedSourceImageMessage);
        }
        catch (ArgumentNullException ex) when (SkiaImageDecodeFailure.IsInvalidImage(ex))
        {
            throw new InvalidDataException(UnsupportedSourceImageMessage, ex);
        }
    }

    private static SKBitmap CreateThumbnailBitmap(
        SKBitmap sourceBitmap,
        PixelSize thumbnailSize)
    {
        SKBitmap thumbnailBitmap = new(
            thumbnailSize.Width,
            thumbnailSize.Height,
            sourceBitmap.ColorType,
            sourceBitmap.AlphaType);

        if ((sourceBitmap.Width == thumbnailSize.Width)
            && (sourceBitmap.Height == thumbnailSize.Height))
        {
            using SKCanvas canvas = new(thumbnailBitmap);
            canvas.DrawBitmap(sourceBitmap, 0, 0);
            canvas.Flush();

            return thumbnailBitmap;
        }

        SKSamplingOptions samplingOptions = new(SKFilterMode.Linear, SKMipmapMode.Linear);

        if (sourceBitmap.ScalePixels(thumbnailBitmap, samplingOptions))
        {
            return thumbnailBitmap;
        }

        thumbnailBitmap.Dispose();
        throw new InvalidOperationException("Thumbnail image could not be resized.");
    }
}
