using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class SkiaAttachedImageCodec : IAttachedImageCodec
{
    private const float FastLosslessWebpEffort = 35f;
    private const float MaximumLosslessWebpEffort = 100f;

    private static readonly SKPngEncoderOptions FastPngEncoderOptions = new(
        SKPngEncoderFilterFlags.AllFilters,
        3);
    private static readonly SKPngEncoderOptions MaximumPngEncoderOptions = new(
        SKPngEncoderFilterFlags.AllFilters,
        9);

    public AttachedImageCodecInfo? ReadInfo(byte[]? content)
    {
        return SkiaAttachedImageDecoder.ReadInfo(content);
    }

    public SKBitmap? Decode(byte[]? content, CancellationToken ct)
    {
        return SkiaAttachedImageDecoder.Decode(content, ct);
    }

    public byte[]? EncodeLosslessly(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        AttachedImageCompressionEffort effort,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return format switch
        {
            AttachedImageEncodingFormat.Webp => EncodeWebp(
                bitmap,
                SKWebpEncoderCompression.Lossless,
                ResolveWebpEffort(effort),
                ct),
            AttachedImageEncodingFormat.Png => EncodePng(
                bitmap,
                ResolvePngOptions(effort),
                ct),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public byte[]? EncodeWithLoss(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        int quality,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        return format switch
        {
            AttachedImageEncodingFormat.Webp => EncodeWebp(
                bitmap,
                SKWebpEncoderCompression.Lossy,
                quality,
                ct),
            AttachedImageEncodingFormat.Jpeg => EncodeJpeg(bitmap, quality, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public SKBitmap Resize(SKBitmap sourceBitmap, SKSizeI targetSize)
    {
        return SkiaAttachedImageDecoder.Resize(sourceBitmap, targetSize);
    }

    private static float ResolveWebpEffort(AttachedImageCompressionEffort effort)
    {
        return effort switch
        {
            AttachedImageCompressionEffort.Fast => FastLosslessWebpEffort,
            AttachedImageCompressionEffort.Maximum => MaximumLosslessWebpEffort,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, null)
        };
    }

    private static SKPngEncoderOptions ResolvePngOptions(AttachedImageCompressionEffort effort)
    {
        return effort switch
        {
            AttachedImageCompressionEffort.Fast => FastPngEncoderOptions,
            AttachedImageCompressionEffort.Maximum => MaximumPngEncoderOptions,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, null)
        };
    }

    private static byte[]? EncodePng(
        SKBitmap bitmap,
        SKPngEncoderOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using SKPixmap pixmap = bitmap.PeekPixels();
        using SKData? data = pixmap.Encode(options);
        ct.ThrowIfCancellationRequested();

        return data?.ToArray();
    }

    private static byte[]? EncodeWebp(
        SKBitmap bitmap,
        SKWebpEncoderCompression compression,
        float quality,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SKWebpEncoderOptions options = new(compression, quality);

        using SKPixmap pixmap = bitmap.PeekPixels();
        using SKData? data = pixmap.Encode(options);
        ct.ThrowIfCancellationRequested();

        return data?.ToArray();
    }

    private static byte[]? EncodeJpeg(SKBitmap bitmap, int quality, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using SKPixmap pixmap = bitmap.PeekPixels();
        using SKData? data = pixmap.Encode(new SKJpegEncoderOptions(quality));
        ct.ThrowIfCancellationRequested();

        return data?.ToArray();
    }
}
