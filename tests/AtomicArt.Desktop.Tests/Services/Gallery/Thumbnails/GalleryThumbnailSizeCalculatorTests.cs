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

    [Fact]
    public void Calculate_WithWideImage_SetsShortSideTo256()
    {
        PixelSize result = GalleryThumbnailSizeCalculator.Calculate(
            GalleryThumbnailSpecification.ShortSidePixels * LargeScale,
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale);

        result.Should().Be(new PixelSize(
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
            GalleryThumbnailSpecification.ShortSidePixels));
    }

    [Fact]
    public void Calculate_WithTallImage_SetsShortSideTo256()
    {
        PixelSize result = GalleryThumbnailSizeCalculator.Calculate(
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
            GalleryThumbnailSpecification.ShortSidePixels * LargeScale);

        result.Should().Be(new PixelSize(
            GalleryThumbnailSpecification.ShortSidePixels,
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale));
    }

    [Fact]
    public void Calculate_WithSmallImage_DoesNotUpscale()
    {
        int smallSide = GalleryThumbnailSpecification.ShortSidePixels / SmallScale;

        PixelSize result = GalleryThumbnailSizeCalculator.Calculate(
            smallSide,
            smallSide);

        result.Should().Be(new PixelSize(smallSide, smallSide));
    }
}
