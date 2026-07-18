using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class PointerDragThresholdTests
{
    [Theory]
    [InlineData(0d, 0d, false)]
    [InlineData(2d, 2d, false)]
    [InlineData(3d, 0d, false)]
    [InlineData(4d, 0d, true)]
    [InlineData(3d, 3d, true)]
    [InlineData(8d, 0d, true)]
    public void IsReached_WithPointerMovement_DetectsActualDrag(
        double x,
        double y,
        bool expectedResult)
    {
        bool result = PointerDragThreshold.IsReached(
            new Point(0d, 0d),
            new Point(x, y));

        result.Should().Be(expectedResult);
    }
}
