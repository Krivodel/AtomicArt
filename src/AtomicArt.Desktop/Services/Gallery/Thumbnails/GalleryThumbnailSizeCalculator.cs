using Avalonia;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal static class GalleryThumbnailSizeCalculator
{
    public static PixelSize Calculate(int width, int height)
    {
        if ((width <= 0) || (height <= 0))
        {
            throw new InvalidDataException(
                "Thumbnail source image dimensions must be positive.");
        }

        int shortSide = Math.Min(width, height);

        if (shortSide <= GalleryThumbnailSpecification.ShortSidePixels)
        {
            return new PixelSize(width, height);
        }

        double scale = (double)GalleryThumbnailSpecification.ShortSidePixels / shortSide;
        int thumbnailWidth = Math.Max(
            1,
            (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
        int thumbnailHeight = Math.Max(
            1,
            (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));

        return new PixelSize(thumbnailWidth, thumbnailHeight);
    }
}
