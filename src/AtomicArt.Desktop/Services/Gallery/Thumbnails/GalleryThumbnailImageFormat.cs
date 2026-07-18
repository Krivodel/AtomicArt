using SkiaSharp;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailImageFormat
{
    private const int PngEncodingQuality = 100;

    public string Extension => GenerationImageFileFormats.PngExtension;
    public int EncodingQuality => PngEncodingQuality;
    public SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Png;
}
