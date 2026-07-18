using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

public interface IAttachedImageCodec
{
    AttachedImageCodecInfo? ReadInfo(byte[]? content);

    SKBitmap? Decode(byte[]? content, CancellationToken ct);

    byte[]? EncodeLosslessly(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        AttachedImageCompressionEffort effort,
        CancellationToken ct);

    byte[]? EncodeWithLoss(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        int quality,
        CancellationToken ct);

    SKBitmap Resize(SKBitmap sourceBitmap, SKSizeI targetSize);
}
