using Avalonia;
using Avalonia.Media.Imaging;

using SkiaSharp;

namespace AtomicArt.Desktop.Services.Imaging;

internal static class PreviewBitmapDecoder
{
    internal static Bitmap Decode(
        Stream stream,
        int maximumShortSidePixels,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            throw new ArgumentException(
                "Preview image stream must support seeking.",
                nameof(stream));
        }

        ct.ThrowIfCancellationRequested();
        stream.Position = 0;
        PixelSize pixelSize = ReadPixelSize(stream, ct);
        PixelSize decodeSize = ImagePreviewSizeCalculator.Calculate(
            pixelSize.Width,
            pixelSize.Height,
            maximumShortSidePixels);
        stream.Position = 0;
        Bitmap bitmap = decodeSize != pixelSize
            ? DecodeReducedBitmap(stream, pixelSize, decodeSize)
            : new Bitmap(stream);

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            ct.ThrowIfCancellationRequested();
        }

        return bitmap;
    }

    private static Bitmap DecodeReducedBitmap(
        Stream stream,
        PixelSize pixelSize,
        PixelSize decodeSize)
    {
        if (pixelSize.Width >= pixelSize.Height)
        {
            return Bitmap.DecodeToHeight(
                stream,
                decodeSize.Height,
                BitmapInterpolationMode.MediumQuality);
        }

        return Bitmap.DecodeToWidth(
            stream,
            decodeSize.Width,
            BitmapInterpolationMode.MediumQuality);
    }

    private static PixelSize ReadPixelSize(Stream stream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using SKManagedStream managedStream = new(stream);
        using SKCodec codec = SKCodec.Create(managedStream)
            ?? throw new InvalidDataException(
                "Preview image dimensions could not be read.");
        SKImageInfo imageInfo = codec.Info;
        bool swapDimensions = codec.EncodedOrigin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
        ct.ThrowIfCancellationRequested();

        return swapDimensions
            ? new PixelSize(imageInfo.Height, imageInfo.Width)
            : new PixelSize(imageInfo.Width, imageInfo.Height);
    }
}
