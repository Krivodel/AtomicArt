using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestInput
{
    internal static void Scroll(Window window)
    {
        Scroll(window, new Vector(0d, -1d));
    }

    internal static void Scroll(Window window, Vector delta)
    {
        window.MouseWheel(
            GetCenterPoint(),
            delta,
            RawInputModifiers.None);
    }

    private static Point GetCenterPoint()
    {
        return new Point(
            SmoothScrollTestConstants.ViewportLength / 2d,
            SmoothScrollTestConstants.ViewportLength / 2d);
    }
}
