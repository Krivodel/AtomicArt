using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailGeneratorTests
{
    private const int LargeScale = 4;
    private const int MediumScale = 2;
    private const int SmallScale = 2;

    [Fact]
    public async Task CreateThumbnailAsync_WithWideImage_SetsShortSideTo256()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithWideImage_SetsShortSideTo256));
        string imagePath = Path.Combine(rootDirectory, "wide.png");
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels * LargeScale,
                GalleryThumbnailSpecification.ShortSidePixels * MediumScale));
        GalleryThumbnailGenerator generator = CreateGenerator();

        byte[] thumbnailBytes = await generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels * MediumScale);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithTallImage_SetsShortSideTo256()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithTallImage_SetsShortSideTo256));
        string imagePath = Path.Combine(rootDirectory, "tall.png");
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
                GalleryThumbnailSpecification.ShortSidePixels * LargeScale));
        GalleryThumbnailGenerator generator = CreateGenerator();

        byte[] thumbnailBytes = await generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels * MediumScale);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithSquareImage_SetsBothSidesTo256()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithSquareImage_SetsBothSidesTo256));
        string imagePath = Path.Combine(rootDirectory, "square.png");
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels * MediumScale,
                GalleryThumbnailSpecification.ShortSidePixels * MediumScale));
        GalleryThumbnailGenerator generator = CreateGenerator();

        byte[] thumbnailBytes = await generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
        size.Height.Should().Be(GalleryThumbnailSpecification.ShortSidePixels);
    }

    [Fact]
    public async Task CreateThumbnailAsync_WithSmallImage_DoesNotUpscale()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_WithSmallImage_DoesNotUpscale));
        string imagePath = Path.Combine(rootDirectory, "small.png");
        int smallSide = GalleryThumbnailSpecification.ShortSidePixels / SmallScale;
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(smallSide, smallSide));
        GalleryThumbnailGenerator generator = CreateGenerator();

        byte[] thumbnailBytes = await generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

        SKSizeI size = GalleryThumbnailTestImages.ReadSize(thumbnailBytes);
        size.Width.Should().Be(smallSide);
        size.Height.Should().Be(smallSide);
    }

    [Fact]
    public async Task CreateThumbnailAsync_UsesThumbnailSpecificationShortSide()
    {
        string rootDirectory = CreateCleanDirectory(nameof(CreateThumbnailAsync_UsesThumbnailSpecificationShortSide));
        string imagePath = Path.Combine(rootDirectory, "spec.png");
        await File.WriteAllBytesAsync(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels * LargeScale,
                GalleryThumbnailSpecification.ShortSidePixels * MediumScale));
        GalleryThumbnailGenerator generator = CreateGenerator();

        byte[] thumbnailBytes = await generator.CreateThumbnailAsync(imagePath, CancellationToken.None);

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

    private static string CreateCleanDirectory(string name)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AtomicArtDesktopTests",
            nameof(GalleryThumbnailGeneratorTests),
            name);

        DeleteDirectoryIfExists(directory);
        Directory.CreateDirectory(directory);

        return directory;
    }

    private static GalleryThumbnailGenerator CreateGenerator()
    {
        return new GalleryThumbnailGenerator(new GalleryThumbnailImageFormat());
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
