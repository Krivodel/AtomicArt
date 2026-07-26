using Avalonia;

using SkiaSharp;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailGenerator : IGalleryThumbnailGenerator
{
    private const int MaximumConcurrentCreations = 1;

    private readonly GalleryThumbnailImageFormat _thumbnailImageFormat;
    private readonly SemaphoreSlim _creationSemaphore =
        new(MaximumConcurrentCreations, MaximumConcurrentCreations);

    public GalleryThumbnailGenerator(GalleryThumbnailImageFormat thumbnailImageFormat)
    {
        ArgumentNullException.ThrowIfNull(thumbnailImageFormat);

        _thumbnailImageFormat = thumbnailImageFormat;
    }

    public async Task<byte[]> CreateThumbnailAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        await _creationSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await Task
                .Run(() => CreateThumbnail(imagePath, ct), ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    private static void EnsureSourceImageSizeIsAllowed(string imagePath)
    {
        FileInfo fileInfo = new(imagePath);

        if (fileInfo.Length > GalleryThumbnailSpecification.MaxSourceImageBytes)
        {
            throw new InvalidDataException("Thumbnail source image exceeds the 500 MB size limit.");
        }
    }

    private byte[] CreateThumbnail(string imagePath, CancellationToken ct)
    {
        EnsureSourceImageSizeIsAllowed(imagePath);
        ct.ThrowIfCancellationRequested();

        using FileStream sourceStream = File.OpenRead(imagePath);
        using SKImage sourceImage = SKImage.FromEncodedData(sourceStream)
            ?? throw new InvalidDataException(
                "Thumbnail source image format is not supported.");
        PixelSize thumbnailSize = GalleryThumbnailSizeCalculator.Calculate(
            sourceImage.Width,
            sourceImage.Height);
        using SKBitmap thumbnailBitmap = new(
            thumbnailSize.Width,
            thumbnailSize.Height,
            SKColorType.Rgba8888,
            sourceImage.AlphaType);
        using SKCanvas canvas = new(thumbnailBitmap);
        SKRect destination = SKRect.Create(
            thumbnailSize.Width,
            thumbnailSize.Height);
        SKSamplingOptions samplingOptions = new(
            SKFilterMode.Linear,
            SKMipmapMode.Linear);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(sourceImage, destination, samplingOptions);
        canvas.Flush();
        ct.ThrowIfCancellationRequested();
        using SKImage thumbnailImage = SKImage.FromBitmap(thumbnailBitmap);
        using SKData encodedImage = thumbnailImage.Encode(
            _thumbnailImageFormat.EncodedFormat,
            _thumbnailImageFormat.EncodingQuality)
            ?? throw new InvalidOperationException(
                "Thumbnail image could not be encoded.");

        return encodedImage.ToArray();
    }
}
