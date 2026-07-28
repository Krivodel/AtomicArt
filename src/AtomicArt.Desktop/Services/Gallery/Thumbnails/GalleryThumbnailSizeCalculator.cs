using Avalonia;

using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailSizeCalculator
{
    private readonly GalleryThumbnailSpecification _specification;

    public GalleryThumbnailSizeCalculator(
        GalleryThumbnailSpecification specification)
    {
        _specification = specification
            ?? throw new ArgumentNullException(nameof(specification));
    }

    public PixelSize Calculate(int width, int height)
    {
        return ImagePreviewSizeCalculator.Calculate(
            width,
            height,
            _specification.ShortSidePixels);
    }
}
