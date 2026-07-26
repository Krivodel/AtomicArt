using Avalonia;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ImageViewerInformationFormatterTests
{
    [Fact]
    public void Format_WithoutPixelSize_ReturnsFileName()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new PixelSize());

        result.Should().Be("image.png");
    }

    [Fact]
    public void Format_WithPixelSize_ReturnsFileNameAndResolution()
    {
        string result = ImageViewerInformationFormatter.Format(
            "image.png",
            new PixelSize(1920, 1080));

        result.Should().Be("image.png · 1920 × 1080");
    }
}
