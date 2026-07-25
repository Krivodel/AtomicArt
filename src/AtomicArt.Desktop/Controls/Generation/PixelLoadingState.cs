using SkiaSharp;

namespace AtomicArt.Desktop.Controls.Generation;

internal readonly record struct PixelLoadingState(
    double InitialPhase,
    SKColor Color,
    double DisappearOrder);
