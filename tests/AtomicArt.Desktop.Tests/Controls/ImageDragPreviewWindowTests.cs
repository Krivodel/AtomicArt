using Avalonia.Media.Imaging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class ImageDragPreviewWindowTests : DesktopControlTestBase
{
    [Fact]
    public void Dispose_WithBorrowedBitmap_LeavesBitmapUsable()
    {
        Dispatch(() =>
        {
            using Bitmap bitmap = CreateBitmap();
            using ImageDragPreviewWindow window =
                ImageDragPreviewWindow.CreateBorrowed(bitmap);

            window.Dispose();

            using MemoryStream output = new();
            bitmap.Save(output);
            output.Length.Should().BePositive();
        });
    }

    private static Bitmap CreateBitmap()
    {
        byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(2, 2);
        using MemoryStream stream = new(bytes);

        return new Bitmap(stream);
    }
}
