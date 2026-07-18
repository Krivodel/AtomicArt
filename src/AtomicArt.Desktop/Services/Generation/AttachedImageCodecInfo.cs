using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record AttachedImageCodecInfo(
    int Width,
    int Height,
    SKAlphaType AlphaType);
