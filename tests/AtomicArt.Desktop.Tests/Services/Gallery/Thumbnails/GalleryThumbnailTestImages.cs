using SkiaSharp;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

internal static class GalleryThumbnailTestImages
{
    private const int TestEncodingQuality = 100;

    public static byte[] CreateJpegBytes(int width, int height, SKColor color)
    {
        return CreateBytes(
            width,
            height,
            color,
            SKEncodedImageFormat.Jpeg);
    }

    public static byte[] CreatePngBytes(int width, int height)
    {
        return CreatePngBytes(width, height, SKColors.CornflowerBlue);
    }

    public static byte[] CreatePngBytes(int width, int height, SKColor color)
    {
        return CreateBytes(
            width,
            height,
            color,
            SKEncodedImageFormat.Png);
    }

    public static SKSizeI ReadSize(byte[] bytes)
    {
        using SKBitmap bitmap = SKBitmap.Decode(bytes)
            ?? throw new InvalidOperationException("Test image could not be decoded.");

        return new SKSizeI(bitmap.Width, bitmap.Height);
    }

    public static SKSizeI ReadSize(string path)
    {
        using SKBitmap bitmap = SKBitmap.Decode(path)
            ?? throw new InvalidOperationException("Test image could not be decoded.");

        return new SKSizeI(bitmap.Width, bitmap.Height);
    }

    private static byte[] CreateBytes(
        int width,
        int height,
        SKColor color,
        SKEncodedImageFormat encodedFormat)
    {
        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(encodedFormat, TestEncodingQuality)
            ?? throw new InvalidOperationException("Test image could not be encoded.");

        return data.ToArray();
    }
}
