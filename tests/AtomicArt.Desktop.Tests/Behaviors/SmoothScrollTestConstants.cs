namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestConstants
{
    internal const double ContentLength = 400d;
    internal const double ViewportLength = 100d;
    internal const double WheelMultiplier = 96d;

    internal static readonly TimeSpan ActiveDuration = TimeSpan.FromMilliseconds(20d);
    internal static readonly TimeSpan DelayMargin = TimeSpan.FromMilliseconds(30d);
    internal static readonly TimeSpan BoundarySeriesDuration = TimeSpan.FromSeconds(1d);
}
