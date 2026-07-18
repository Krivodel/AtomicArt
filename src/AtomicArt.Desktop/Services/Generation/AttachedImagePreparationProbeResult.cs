using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed record AttachedImagePreparationProbeResult(
    SKSizeI WorkingSize,
    bool IsLosslessEncodingPromising);
