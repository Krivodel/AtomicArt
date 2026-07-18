using Avalonia;

namespace AtomicArt.Desktop.Controls;

internal static class PointerDragThreshold
{
    private const double MinimumDistance = 4d;

    internal static bool IsReached(Point origin, Point current)
    {
        double dx = current.X - origin.X;
        double dy = current.Y - origin.Y;

        return Math.Sqrt((dx * dx) + (dy * dy)) >= MinimumDistance;
    }
}
