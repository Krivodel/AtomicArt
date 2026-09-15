using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

internal static class Dlss5BitmapPreparation
{
    private const double DefaultDpi = 96d;

    public static Bitmap CreateAvaloniaBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        SKBitmap? convertedBitmap = null;
        SKBitmap displayBitmap = bitmap;
        if (bitmap.ColorType != SKColorType.Rgba8888)
        {
            convertedBitmap = bitmap.Copy(SKColorType.Rgba8888)
                ?? throw new InvalidDataException(
                    $"The image cannot be converted to RGBA pixels from {bitmap.ColorType}.");
            displayBitmap = convertedBitmap;
        }

        try
        {
            AlphaFormat alphaFormat = displayBitmap.AlphaType switch
            {
                SKAlphaType.Premul => AlphaFormat.Premul,
                SKAlphaType.Unpremul => AlphaFormat.Unpremul,
                SKAlphaType.Opaque => AlphaFormat.Opaque,
                _ => throw new InvalidDataException(
                    $"The image has an unsupported alpha format: {displayBitmap.AlphaType}.")
            };

            // Avalonia copies the supplied pixel buffer while constructing its
            // platform bitmap. This avoids the expensive image encode/decode
            // round-trip on every completed render or source replacement.
            return new Bitmap(
                PixelFormat.Rgba8888,
                alphaFormat,
                displayBitmap.GetPixels(),
                new PixelSize(displayBitmap.Width, displayBitmap.Height),
                new Vector(DefaultDpi, DefaultDpi),
                displayBitmap.RowBytes);
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }
}
