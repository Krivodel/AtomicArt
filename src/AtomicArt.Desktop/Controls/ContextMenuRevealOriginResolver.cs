using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AtomicArt.Desktop.Controls;

internal static class ContextMenuRevealOriginResolver
{
    public static ContextMenuRevealOrigin Resolve(Visual menuVisual)
    {
        ArgumentNullException.ThrowIfNull(menuVisual);

        TopLevel? topLevel = TopLevel.GetTopLevel(menuVisual);
        Screens? screens = topLevel?.Screens;
        Screen? screen = screens?.ScreenFromVisual(menuVisual);
        if (topLevel is null
            || screen is null
            || (menuVisual.Bounds.Width <= 0d)
            || (menuVisual.Bounds.Height <= 0d))
        {
            return ContextMenuRevealOrigin.TopLeft;
        }

        PixelPoint topLeft = menuVisual.PointToScreen(new Point());
        PixelSize pixelSize = PixelSize.FromSize(
            menuVisual.Bounds.Size,
            topLevel.RenderScaling);
        PixelRect menuBounds = new(topLeft, pixelSize);

        return Resolve(screen.WorkingArea, menuBounds);
    }

    public static ContextMenuRevealOrigin Resolve(
        PixelRect workingArea,
        PixelRect menuBounds)
    {
        int remainingWidth = workingArea.Right - menuBounds.Right;
        int remainingHeight = workingArea.Bottom - menuBounds.Bottom;
        bool revealFromRight = remainingWidth < menuBounds.Width;
        bool revealFromBottom = remainingHeight < menuBounds.Height;

        if (revealFromRight)
        {
            return revealFromBottom
                ? ContextMenuRevealOrigin.BottomRight
                : ContextMenuRevealOrigin.TopRight;
        }

        return revealFromBottom
            ? ContextMenuRevealOrigin.BottomLeft
            : ContextMenuRevealOrigin.TopLeft;
    }
}
