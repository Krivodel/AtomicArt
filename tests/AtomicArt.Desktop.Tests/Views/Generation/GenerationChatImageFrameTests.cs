using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Tests.Views.Generation;

public sealed class GenerationChatImageFrameTests
{
    [Theory]
    [InlineData(1024, 1024, 232, 440, 232, 232)]
    [InlineData(2160, 3840, 470, 440, 247.5, 440)]
    [InlineData(3840, 2160, 232, 440, 232, 130.5)]
    [InlineData(2100, 900, 420, 440, 420, 180)]
    [InlineData(100, 1000, 232, 440, 44, 440)]
    [InlineData(0, 0, 232, 440, 232, 232)]
    public void Fit_WithDifferentImageSizes_FitsFrameWithoutCropping(
        double width, double height, double availableWidth, double availableHeight,
        double expectedWidth, double expectedHeight)
    {
        Size result = GenerationChatImageFrame.Fit(new Size(availableWidth, availableHeight), new Size(width, height));

        result.Width.Should().BeApproximately(expectedWidth, 0.01d);
        result.Height.Should().BeApproximately(expectedHeight, 0.01d);
    }
}
