using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class FreeWindowResizeSessionTests
{
    [Fact]
    public void Calculate_WhenBottomRightCornerMoves_ChangesAxesIndependently()
    {
        WindowRectangle initialRectangle = new()
        {
            Left = 100,
            Top = 100,
            Right = 900,
            Bottom = 600
        };
        FreeWindowResizeSession session = new(
            initialRectangle,
            new PixelPoint(900, 600),
            WindowSizingEdge.BottomRight);

        WindowRectangle result = session.Calculate(new PixelPoint(1000, 650));

        result.Left.Should().Be(100);
        result.Top.Should().Be(100);
        result.Right.Should().Be(1000);
        result.Bottom.Should().Be(650);
    }

    [Fact]
    public void Calculate_WhenLeftEdgeExceedsMinimum_KeepsMinimumWidthAndRightEdge()
    {
        WindowRectangle initialRectangle = new()
        {
            Left = 100,
            Top = 100,
            Right = 900,
            Bottom = 600
        };
        FreeWindowResizeSession session = new(
            initialRectangle,
            new PixelPoint(100, 350),
            WindowSizingEdge.Left);

        WindowRectangle result = session.Calculate(new PixelPoint(1000, 350));

        result.Width.Should().Be(64);
        result.Right.Should().Be(900);
    }
}
