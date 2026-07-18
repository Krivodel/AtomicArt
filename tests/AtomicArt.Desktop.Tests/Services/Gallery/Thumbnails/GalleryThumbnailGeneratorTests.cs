using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailGeneratorTests
{
    private const int LargeScale = 4;
    private const int MediumScale = 2;
    private const int SmallScale = 2;

    [Fact]
    public async Task CreateThumbnailAsync_WithWideImage_SetsShortSideTo256()
    {
        byte[] thumbnailBytes = await CreateThumbnailAsync(
            nameof(CreateThumbnailAsync_WithWideImage_SetsShortSideTo256),
            "wide.png",
            GalleryThumbnailSpecification.ShortSidePixels * LargeScale,
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels * MediumScale);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithTallImage_SetsShortSideTo256()
    {
        byte[] thumbnailBytes = await CreateThumbnailAsync(
            nameof(CreateThumbnailAsync_WithTallImage_SetsShortSideTo256),
            "tall.png",
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
            GalleryThumbnailSpecification.ShortSidePixels * LargeScale);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels * MediumScale);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithSquareImage_SetsBothSidesTo256()
    {
        byte[] thumbnailBytes = await CreateThumbnailAsync(
            nameof(CreateThumbnailAsync_WithSquareImage_SetsBothSidesTo256),
            "square.png",
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithSmallImage_DoesNotUpscale()
    {
        int smallSide = GalleryThumbnailSpecification.ShortSidePixels / SmallScale;

        byte[] thumbnailBytes = await CreateThumbnailAsync(
            nameof(CreateThumbnailAsync_WithSmallImage_DoesNotUpscale),
            "small.png",
            smallSide,
            smallSide);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(smallSide);
        size.Height.Should().Be(smallSide);
    }

    [Fact]
    public async Task CreateThumbnailAsync_UsesThumbnailSpecificationShortSide()
    {
        byte[] thumbnailBytes = await CreateThumbnailAsync(
            nameof(CreateThumbnailAsync_UsesThumbnailSpecificationShortSide),
            "spec.png",
            GalleryThumbnailSpecification.ShortSidePixels * LargeScale,
            GalleryThumbnailSpecification.ShortSidePixels * MediumScale);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        Math.Min(size.Width, size.Height).Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithInvalidImage_ThrowsInvalidDataException()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithInvalidImage_ThrowsInvalidDataException));
        string imagePath = Path.Combine(rootDirectory, "invalid.png");
        await File.WriteAllBytesAsync(imagePath, [0x01, 0x02, 0x03]);
        GalleryThumbnailGenerator generator = CreateGenerator();

        Func<Task> act = () => generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithImageLargerThanLimit_ThrowsInvalidDataException()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithImageLargerThanLimit_ThrowsInvalidDataException));
        string imagePath = Path.Combine(rootDirectory, "oversized.png");
        await using (FileStream stream = new(
            imagePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.SetLength(GalleryThumbnailSpecification.MaxSourceImageBytes + 1);
        }
        GalleryThumbnailGenerator generator = CreateGenerator();

        Func<Task> act = () => generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*500 MB*");
    }

    private static GalleryThumbnailGenerator CreateGenerator()
    {
        return new GalleryThumbnailGenerator(new GalleryThumbnailImageFormat());
    }

    private static async Task<byte[]> CreateThumbnailAsync(
        string testName,
        string fileName,
        int width,
        int height)
    {
        string rootDirectory = CreateCleanDirectory(testName);
        string imagePath = Path.Combine(rootDirectory, fileName);
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(width, height))
            .ConfigureAwait(false);

        GalleryThumbnailGenerator generator = CreateGenerator();

        return await generator.CreateThumbnailAsync(
            imagePath,
            CancellationToken.None)
            .ConfigureAwait(false);
    }

}
