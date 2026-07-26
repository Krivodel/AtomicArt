using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Windowing;

namespace AtomicArt.Desktop.Tests.Services.Windowing;

public sealed class WindowPlacementStateTests
{
    [Fact]
    public void CreateNormalized_WithIncompleteValues_RemovesIncompletePairs()
    {
        WindowPlacementState state = new()
        {
            X = 120,
            Width = 800d,
            IsMaximized = true
        };

        WindowPlacementState normalizedState = state.CreateNormalized();

        normalizedState.X.Should().BeNull();
        normalizedState.Y.Should().BeNull();
        normalizedState.Width.Should().BeNull();
        normalizedState.Height.Should().BeNull();
        normalizedState.IsMaximized.Should().BeTrue();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void CreateNormalized_WithInvalidDimensions_RemovesSize(
        double invalidDimension)
    {
        WindowPlacementState state = new()
        {
            Width = invalidDimension,
            Height = 600d
        };

        WindowPlacementState normalizedState = state.CreateNormalized();

        normalizedState.Width.Should().BeNull();
        normalizedState.Height.Should().BeNull();
    }
}
