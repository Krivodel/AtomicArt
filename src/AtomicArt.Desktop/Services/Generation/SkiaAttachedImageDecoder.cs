using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

internal static class SkiaAttachedImageDecoder
{
    private static readonly SKSamplingOptions ResizeSamplingOptions = new(
        SKFilterMode.Linear,
        SKMipmapMode.Linear);

    public static AttachedImageCodecInfo? ReadInfo(byte[]? content)
    {
        if (content is null || content.Length == 0)
        {
            return null;
        }

        try
        {
            using MemoryStream input = new(content, writable: false);
            using SKManagedStream stream = new(input);
            using SKCodec? codec = SKCodec.Create(stream);

            if (codec is null)
            {
                return null;
            }

            SKImageInfo info = codec.Info;

            return new AttachedImageCodecInfo(
                info.Width,
                info.Height,
                info.AlphaType);
        }
        catch (ArgumentNullException ex) when (string.Equals(
            ex.ParamName,
            "codec",
            StringComparison.Ordinal))
        {
            return null;
        }
    }

    public static SKBitmap? Decode(byte[]? content, CancellationToken ct)
    {
        if (content is null || content.Length == 0)
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        SKBitmap? bitmap;

        try
        {
            bitmap = SKBitmap.Decode(content);
        }
        catch (ArgumentNullException ex) when (string.Equals(
            ex.ParamName,
            "codec",
            StringComparison.Ordinal))
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        return bitmap;
    }

    public static SKBitmap Resize(SKBitmap sourceBitmap, SKSizeI targetSize)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);

        SKBitmap targetBitmap = new(
            targetSize.Width,
            targetSize.Height,
            sourceBitmap.ColorType,
            sourceBitmap.AlphaType);

        if (sourceBitmap.ScalePixels(targetBitmap, ResizeSamplingOptions))
        {
            return targetBitmap;
        }

        targetBitmap.Dispose();
        throw new InvalidOperationException("Attached image could not be resized.");
    }
}
