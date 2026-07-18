using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowSizingEdgesExtensionsTests
{
    [Theory]
    [InlineData((int)WindowSizingEdges.Left, true)]
    [InlineData((int)WindowSizingEdges.Right, true)]
    [InlineData((int)WindowSizingEdges.Top, false)]
    [InlineData((int)WindowSizingEdges.Bottom, false)]
    [InlineData((int)WindowSizingEdges.TopLeft, true)]
    [InlineData((int)WindowSizingEdges.TopRight, true)]
    [InlineData((int)WindowSizingEdges.BottomLeft, true)]
    [InlineData((int)WindowSizingEdges.BottomRight, true)]
    public void IncludesHorizontalEdge_WithSizingEdges_ReturnsExpectedResult(
        int sizingEdgesValue,
        bool expectedResult)
    {
        WindowSizingEdges sizingEdges = (WindowSizingEdges)sizingEdgesValue;

        bool result = sizingEdges.IncludesHorizontalEdge();

        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData((int)WindowSizingEdges.Left, false)]
    [InlineData((int)WindowSizingEdges.Right, false)]
    [InlineData((int)WindowSizingEdges.Top, true)]
    [InlineData((int)WindowSizingEdges.Bottom, true)]
    [InlineData((int)WindowSizingEdges.TopLeft, true)]
    [InlineData((int)WindowSizingEdges.TopRight, true)]
    [InlineData((int)WindowSizingEdges.BottomLeft, true)]
    [InlineData((int)WindowSizingEdges.BottomRight, true)]
    public void IncludesVerticalEdge_WithSizingEdges_ReturnsExpectedResult(
        int sizingEdgesValue,
        bool expectedResult)
    {
        WindowSizingEdges sizingEdges = (WindowSizingEdges)sizingEdgesValue;

        bool result = sizingEdges.IncludesVerticalEdge();

        result.Should().Be(expectedResult);
    }
}
