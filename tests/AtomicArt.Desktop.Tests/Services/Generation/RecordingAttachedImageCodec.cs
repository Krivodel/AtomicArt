using SkiaSharp;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class RecordingAttachedImageCodec : IAttachedImageCodec
{
    private const int BitmapWidth = 32;
    private const int BitmapHeight = 32;

    public AttachedImageCodecInfo ImageInfo { get; set; } = new(
        BitmapWidth,
        BitmapHeight,
        SKAlphaType.Opaque);
    public int DecodeCallCount { get; private set; }
    public List<(AttachedImageEncodingFormat Format, AttachedImageCompressionEffort Effort)>
        LosslessCalls { get; } = [];
    public List<(AttachedImageEncodingFormat Format, int Quality)> LossyCalls { get; } = [];
    public List<SKSizeI> ResizeCalls { get; } = [];
    public Func<AttachedImageCompressionEffort, byte[]?> LosslessEncoder { get; set; } =
        _ => [];
    public Func<int, byte[]?> LossyEncoder { get; set; } =
        _ => [];

    public AttachedImageCodecInfo ReadInfo(byte[]? content)
    {
        _ = content;

        return ImageInfo;
    }

    public SKBitmap Decode(byte[]? content, CancellationToken ct)
    {
        _ = content;
        ct.ThrowIfCancellationRequested();
        DecodeCallCount++;

        return new SKBitmap(
            BitmapWidth,
            BitmapHeight,
            SKColorType.Rgba8888,
            ImageInfo.AlphaType);
    }

    public byte[]? EncodeLosslessly(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        AttachedImageCompressionEffort effort,
        CancellationToken ct)
    {
        _ = bitmap;
        ct.ThrowIfCancellationRequested();
        LosslessCalls.Add((format, effort));

        return LosslessEncoder(effort);
    }

    public byte[]? EncodeWithLoss(
        SKBitmap bitmap,
        AttachedImageEncodingFormat format,
        int quality,
        CancellationToken ct)
    {
        _ = bitmap;
        ct.ThrowIfCancellationRequested();
        LossyCalls.Add((format, quality));

        return LossyEncoder(quality);
    }

    public SKBitmap Resize(SKBitmap sourceBitmap, SKSizeI targetSize)
    {
        _ = sourceBitmap;
        ResizeCalls.Add(targetSize);

        return new SKBitmap(
            BitmapWidth,
            BitmapHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);
    }
}
