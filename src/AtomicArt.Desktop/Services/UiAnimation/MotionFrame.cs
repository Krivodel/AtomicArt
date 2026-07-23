namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed record MotionFrame(
    double X,
    double Y,
    double Scale,
    double Rotate,
    double Opacity)
{
    internal static MotionFrame Identity { get; } = new(0d, 0d, 1d, 0d, 1d);
}
