using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Media.Imaging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Controls.Gallery;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryPreviewBitmapLoaderTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public async Task LoadAsync_WithValidImage_ReturnsBitmap()
    {
        await DispatchAsync(async () =>
        {
            string rootDirectory = CreateCleanDirectory(
                nameof(LoadAsync_WithValidImage_ReturnsBitmap));
            string imagePath = Path.Combine(rootDirectory, "image.png");
            await File.WriteAllBytesAsync(
                imagePath,
                GalleryThumbnailTestImages.CreatePngBytes(512, 256));
            GalleryPreviewBitmapLoader loader = CreateLoader();

            using Bitmap? bitmap = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);

            bitmap.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task LoadAsync_WithInvalidImage_ReturnsNull()
    {
        await DispatchAsync(async () =>
        {
            string rootDirectory = CreateCleanDirectory(
                nameof(LoadAsync_WithInvalidImage_ReturnsNull));
            string imagePath = Path.Combine(rootDirectory, "invalid.png");
            await File.WriteAllBytesAsync(imagePath, [0x01, 0x02, 0x03]);
            GalleryPreviewBitmapLoader loader = CreateLoader();

            using Bitmap? bitmap = await loader.LoadAsync(
                imagePath,
                CancellationToken.None);

            bitmap.Should().BeNull();
        });
    }

    private static GalleryPreviewBitmapLoader CreateLoader()
    {
        return new GalleryPreviewBitmapLoader(
            NullLogger<GalleryPreviewBitmapLoader>.Instance,
            TestApiConfiguration.CreateGalleryThumbnailSpecification(),
            TestApiConfiguration.CreateGalleryOptionsWrapper());
    }
}
