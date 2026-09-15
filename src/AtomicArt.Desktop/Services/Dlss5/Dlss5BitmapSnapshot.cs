using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

internal static class Dlss5BitmapSnapshot
{
    public static SKImage CreateImage(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SKImage.FromBitmap(source)
            ?? throw new InvalidDataException("The DLSS 5 source image cannot be snapshotted.");
    }

    public static SKBitmap CreateBitmap(SKImage source)
    {
        ArgumentNullException.ThrowIfNull(source);

        SKBitmap bitmap = new(source.Info);
        if (!source.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes))
        {
            bitmap.Dispose();
            throw new InvalidDataException("The DLSS 5 source snapshot cannot be read.");
        }

        return bitmap;
    }
}
