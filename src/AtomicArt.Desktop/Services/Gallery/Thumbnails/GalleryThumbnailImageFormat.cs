using SkiaSharp;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailImageFormat
{
    private const string PngExtension = ".png";
    private const int PngEncodingQuality = 100;

    public string Extension => PngExtension;
    public int EncodingQuality => PngEncodingQuality;
    public SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Png;
}
