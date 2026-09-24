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
        PixelRect? menuBounds = GetScreenBounds(menuVisual);
        if (screen is null || menuBounds is null)
        {
            return ContextMenuRevealOrigin.TopLeft;
        }

        return Resolve(screen.WorkingArea, menuBounds.Value);
    }

    public static ContextMenuRevealOrigin ResolveSubmenu(
        Visual parentMenuItem,
        Visual submenu)
    {
        ArgumentNullException.ThrowIfNull(parentMenuItem);
        ArgumentNullException.ThrowIfNull(submenu);

        PixelRect? parentBounds = GetScreenBounds(parentMenuItem);
        PixelRect? submenuBounds = GetScreenBounds(submenu);
        if (parentBounds is null || submenuBounds is null)
        {
            return Resolve(submenu);
        }

        return ResolveSubmenu(parentBounds.Value, submenuBounds.Value);
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

    public static ContextMenuRevealOrigin ResolveSubmenu(
        PixelRect parentBounds,
        PixelRect submenuBounds)
    {
        bool revealFromRight = submenuBounds.X + (submenuBounds.Width / 2d)
            < parentBounds.X + (parentBounds.Width / 2d);
        bool revealFromBottom = submenuBounds.Y + (submenuBounds.Height / 2d)
            < parentBounds.Y + (parentBounds.Height / 2d);

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

    private static PixelRect? GetScreenBounds(Visual visual)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(visual);
        if (topLevel is null
            || (visual.Bounds.Width <= 0d)
            || (visual.Bounds.Height <= 0d))
        {
            return null;
        }

        PixelPoint topLeft = visual.PointToScreen(new Point());
        PixelSize pixelSize = PixelSize.FromSize(
            visual.Bounds.Size,
            topLevel.RenderScaling);

        return new PixelRect(topLeft, pixelSize);
    }
}
