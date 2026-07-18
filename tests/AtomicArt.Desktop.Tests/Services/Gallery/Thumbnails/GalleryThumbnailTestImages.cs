using SkiaSharp;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

internal static class GalleryThumbnailTestImages
{
    public static byte[] CreatePngBytes(int width, int height)
    {
        return CreatePngBytes(width, height, SKColors.CornflowerBlue);
    }

    public static byte[] CreatePngBytes(int width, int height, SKColor color)
    {
        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Test image could not be encoded.");

        return data.ToArray();
    }

    public static SKColor ReadFirstPixel(string path)
    {
        using SKBitmap bitmap = SKBitmap.Decode(path)
            ?? throw new InvalidOperationException("Test image could not be decoded.");

        return bitmap.GetPixel(0, 0);
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
}
