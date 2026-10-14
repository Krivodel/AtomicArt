using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Windowing;

namespace AtomicArt.Desktop.Tests.Services.Windowing;

public sealed class GenerationPanelLayoutStateTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void CreateNormalized_WithInvalidDimension_PreservesOtherAxis(double invalidDimension)
    {
        GenerationPanelLayoutState invalidWidth = new() { Width = invalidDimension, Height = 400d };
        GenerationPanelLayoutState invalidHeight = new() { Width = 1000d, Height = invalidDimension };

        GenerationPanelLayoutState widthState = invalidWidth.CreateNormalized();
        GenerationPanelLayoutState heightState = invalidHeight.CreateNormalized();

        widthState.Width.Should().BeNull();
        widthState.Height.Should().Be(400d);
        heightState.Width.Should().Be(1000d);
        heightState.Height.Should().BeNull();
    }
}
