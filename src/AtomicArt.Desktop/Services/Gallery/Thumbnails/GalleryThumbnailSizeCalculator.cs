using Avalonia;

using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal static class GalleryThumbnailSizeCalculator
{
    public static PixelSize Calculate(int width, int height)
    {
        return ImagePreviewSizeCalculator.Calculate(
            width,
            height,
            GalleryThumbnailSpecification.ShortSidePixels);
    }
}
