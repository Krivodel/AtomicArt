using SkiaSharp;

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
        SKSizeI thumbnailSize = CalculateThumbnailSize(sourceBitmap.Width, sourceBitmap.Height);
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

    private static SKSizeI CalculateThumbnailSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Thumbnail source image dimensions must be positive.");
        }

        int shortSide = Math.Min(width, height);

        if (shortSide <= GalleryThumbnailSpecification.ShortSidePixels)
        {
            return new SKSizeI(width, height);
        }

        double scale = (double)GalleryThumbnailSpecification.ShortSidePixels / shortSide;
        int thumbnailWidth = Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
        int thumbnailHeight = Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));

        return new SKSizeI(thumbnailWidth, thumbnailHeight);
    }

    private static SKBitmap DecodeSourceBitmap(byte[] sourceBytes)
    {
        try
        {
            return SKBitmap.Decode(sourceBytes)
                ?? throw new InvalidDataException(UnsupportedSourceImageMessage);
        }
        catch (ArgumentNullException ex) when (string.Equals(ex.ParamName, "codec", StringComparison.Ordinal))
        {
            throw new InvalidDataException(UnsupportedSourceImageMessage, ex);
        }
    }

    private static SKBitmap CreateThumbnailBitmap(SKBitmap sourceBitmap, SKSizeI thumbnailSize)
    {
        SKBitmap thumbnailBitmap = new(
            thumbnailSize.Width,
            thumbnailSize.Height,
            sourceBitmap.ColorType,
            sourceBitmap.AlphaType);

        if (sourceBitmap.Width == thumbnailSize.Width && sourceBitmap.Height == thumbnailSize.Height)
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
