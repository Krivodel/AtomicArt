using Avalonia;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class ContextMenuRevealOriginResolverTests
{
    private static readonly PixelRect WorkingArea = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(400, 300, (int)ContextMenuRevealOrigin.TopLeft)]
    [InlineData(1670, 300, (int)ContextMenuRevealOrigin.TopRight)]
    [InlineData(400, 870, (int)ContextMenuRevealOrigin.BottomLeft)]
    [InlineData(1670, 870, (int)ContextMenuRevealOrigin.BottomRight)]
    public void Resolve_WithMenuPosition_SelectsExpectedCorner(
        int x,
        int y,
        int expectedOriginValue)
    {
        PixelRect menuBounds = new(x, y, 240, 200);
        ContextMenuRevealOrigin expectedOrigin = (ContextMenuRevealOrigin)expectedOriginValue;

        ContextMenuRevealOrigin origin = ContextMenuRevealOriginResolver.Resolve(
            WorkingArea,
            menuBounds);

        origin.Should().Be(expectedOrigin);
    }

    [Theory]
    [InlineData(300, 200, (int)ContextMenuRevealOrigin.TopLeft)]
    [InlineData(0, 200, (int)ContextMenuRevealOrigin.TopRight)]
    [InlineData(300, 20, (int)ContextMenuRevealOrigin.BottomLeft)]
    [InlineData(0, 20, (int)ContextMenuRevealOrigin.BottomRight)]
    public void ResolveSubmenu_WithRelativePosition_RevealsFromAttachedCorner(
        int submenuX,
        int submenuY,
        int expectedOriginValue)
    {
        PixelRect parentBounds = new(200, 200, 100, 40);
        PixelRect submenuBounds = new(submenuX, submenuY, 200, 160);
        ContextMenuRevealOrigin expectedOrigin = (ContextMenuRevealOrigin)expectedOriginValue;

        ContextMenuRevealOrigin origin = ContextMenuRevealOriginResolver.ResolveSubmenu(
            parentBounds,
            submenuBounds);

        origin.Should().Be(expectedOrigin);
    }
}
