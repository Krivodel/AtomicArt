using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

internal static class AvaloniaAttachmentBitmapCopy
{
    public static SKBitmap Create(Bitmap bitmap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ct.ThrowIfCancellationRequested();

        SKColorType colorType = bitmap.Format switch
        {
            PixelFormat format when format == PixelFormat.Rgba8888 => SKColorType.Rgba8888,
            PixelFormat format when format == PixelFormat.Bgra8888 => SKColorType.Bgra8888,
            _ => throw new NotSupportedException(
                $"Attachment bitmap pixel format '{bitmap.Format}' is unsupported.")
        };
        SKAlphaType alphaType = bitmap.AlphaFormat switch
        {
            AlphaFormat.Premul => SKAlphaType.Premul,
            AlphaFormat.Unpremul => SKAlphaType.Unpremul,
            AlphaFormat.Opaque => SKAlphaType.Opaque,
            _ => throw new NotSupportedException(
                $"Attachment bitmap alpha format '{bitmap.AlphaFormat}' is unsupported.")
        };
        PixelSize size = bitmap.PixelSize;
        SKImageInfo imageInfo = new(size.Width, size.Height, colorType, alphaType);
        SKBitmap copy = new(imageInfo);
        bool completed = false;

        try
        {
            PixelRect sourceRect = new(0, 0, size.Width, size.Height);
            int bufferSize = checked(copy.RowBytes * size.Height);
            bitmap.CopyPixels(sourceRect, copy.GetPixels(), bufferSize, copy.RowBytes);
            ct.ThrowIfCancellationRequested();
            completed = true;

            return copy;
        }
        finally
        {
            if (!completed)
            {
                copy.Dispose();
            }
        }
    }
}
