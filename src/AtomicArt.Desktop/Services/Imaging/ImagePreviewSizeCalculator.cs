using Avalonia;

namespace AtomicArt.Desktop.Services.Imaging;

internal static class ImagePreviewSizeCalculator
{
    internal static PixelSize Calculate(
        int width,
        int height,
        int maximumShortSidePixels)
    {
        if ((width <= 0) || (height <= 0))
        {
            throw new InvalidDataException(
                "Preview source image dimensions must be positive.");
        }

        if (maximumShortSidePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumShortSidePixels),
                maximumShortSidePixels,
                "Maximum preview short side must be positive.");
        }

        int shortSide = Math.Min(width, height);

        if (shortSide <= maximumShortSidePixels)
        {
            return new PixelSize(width, height);
        }

        double scale = (double)maximumShortSidePixels / shortSide;
        int previewWidth = Math.Max(
            1,
            (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
        int previewHeight = Math.Max(
            1,
            (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));

        return new PixelSize(previewWidth, previewHeight);
    }
}
