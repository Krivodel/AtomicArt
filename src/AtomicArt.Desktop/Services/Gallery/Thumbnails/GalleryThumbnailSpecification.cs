using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailSpecification
{
    public const int ThumbnailShortSidePixels = 256;

    public int ShortSidePixels => ThumbnailShortSidePixels;
    public long MaximumSourceImageBytes { get; }

    public GalleryThumbnailSpecification(IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        MaximumSourceImageBytes = options.Value.MaximumThumbnailSourceImageBytes;
    }
}
