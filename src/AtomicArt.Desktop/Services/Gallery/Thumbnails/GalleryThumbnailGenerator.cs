using Microsoft.Extensions.Options;

using Avalonia;

using SkiaSharp;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailGenerator : IGalleryThumbnailGenerator
{
    private const long BytesPerMegabyte = 1_048_576L;

    private readonly GalleryThumbnailImageFormat _thumbnailImageFormat;
    private readonly GalleryThumbnailSizeCalculator _sizeCalculator;
    private readonly long _maximumSourceImageBytes;
    private readonly SemaphoreSlim _creationSemaphore;

    public GalleryThumbnailGenerator(
        GalleryThumbnailImageFormat thumbnailImageFormat,
        GalleryThumbnailSizeCalculator sizeCalculator,
        GalleryThumbnailSpecification specification,
        IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(thumbnailImageFormat);
        ArgumentNullException.ThrowIfNull(sizeCalculator);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(options);

        _thumbnailImageFormat = thumbnailImageFormat;
        _sizeCalculator = sizeCalculator;
        _maximumSourceImageBytes = specification.MaximumSourceImageBytes;
        _creationSemaphore = new SemaphoreSlim(
            options.Value.MaximumThumbnailCreationConcurrency,
            options.Value.MaximumThumbnailCreationConcurrency);
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

    private void EnsureSourceImageSizeIsAllowed(string imagePath)
    {
        FileInfo fileInfo = new(imagePath);

        if (fileInfo.Length > _maximumSourceImageBytes)
        {
            long maximumSourceImageMegabytes =
                _maximumSourceImageBytes / BytesPerMegabyte;

            throw new InvalidDataException(
                $"Thumbnail source image exceeds the {maximumSourceImageMegabytes} MB size limit.");
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
        PixelSize thumbnailSize = _sizeCalculator.Calculate(
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
