using Avalonia;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailSizeCalculatorTests
{
    private const int LargeScale = 4;
    private const int MediumScale = 2;
    private const int SmallScale = 2;

    private readonly GalleryThumbnailSizeCalculator _calculator = new(
        TestApiConfiguration.CreateGalleryThumbnailSpecification());

    [Fact]
    public void Calculate_WithWideImage_SetsShortSideTo256()
    {
        PixelSize result = _calculator.Calculate(
            TestApiConfiguration.ThumbnailShortSidePixels * LargeScale,
            TestApiConfiguration.ThumbnailShortSidePixels * MediumScale);

        result.Should().Be(new PixelSize(
            TestApiConfiguration.ThumbnailShortSidePixels * MediumScale,
            TestApiConfiguration.ThumbnailShortSidePixels));
    }

    [Fact]
    public void Calculate_WithTallImage_SetsShortSideTo256()
    {
        PixelSize result = _calculator.Calculate(
            TestApiConfiguration.ThumbnailShortSidePixels * MediumScale,
            TestApiConfiguration.ThumbnailShortSidePixels * LargeScale);

        result.Should().Be(new PixelSize(
            TestApiConfiguration.ThumbnailShortSidePixels,
            TestApiConfiguration.ThumbnailShortSidePixels * MediumScale));
    }

    [Fact]
    public void Calculate_WithSmallImage_DoesNotUpscale()
    {
        int smallSide = TestApiConfiguration.ThumbnailShortSidePixels / SmallScale;

        PixelSize result = _calculator.Calculate(
            smallSide,
            smallSide);

        result.Should().Be(new PixelSize(smallSide, smallSide));
    }
}
