using SkiaSharp;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailImageFormat
{
    public const int JpegEncodingQuality = 90;

    public string Extension => GenerationImageFileFormats.JpegExtension;
    public int EncodingQuality => JpegEncodingQuality;
    public SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Jpeg;
}
