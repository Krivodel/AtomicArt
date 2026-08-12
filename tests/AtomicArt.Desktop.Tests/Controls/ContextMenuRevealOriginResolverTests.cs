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
}
