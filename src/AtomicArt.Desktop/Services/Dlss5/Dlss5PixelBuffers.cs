using System.Runtime.InteropServices;

using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

internal sealed class Dlss5PixelBuffers
{
    public int Width { get; }
    public int Height { get; }
    public byte[] InputRgba { get; }
    public byte[] OutputRgba { get; }
    public byte[] Motion { get; }

    private const int MinimumDimension = 64;
    private const int RgbaBytesPerPixel = 4;
    private const int MotionComponents = 2;
    private const int MotionBytesPerComponent = 2;
    private const int RedChannelOffset = 0;
    private const int GreenChannelOffset = 1;
    private const int BlueChannelOffset = 2;
    private const int AlphaChannelOffset = 3;
    private const int IntegerRoundingDivisor = 2;

    private Dlss5PixelBuffers(int width, int height, byte[] inputRgba)
    {
        Width = width;
        Height = height;
        InputRgba = inputRgba;
        OutputRgba = GC.AllocateUninitializedArray<byte>(inputRgba.Length);
        Motion = GC.AllocateUninitializedArray<byte>(
            checked(width * height * MotionComponents * MotionBytesPerComponent));
        Motion.AsSpan().Clear();
    }

    public static Dlss5PixelBuffers Create(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Width, MinimumDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Height, MinimumDimension);

        // The v7 Python path feeds straight (unassociated) RGBA8 bytes to the
        // worker. Normalize Skia's frequently premultiplied decode to the same
        // contract so translucent inputs cannot change the neural result.
        using SKBitmap rgba = source.ColorType == SKColorType.Rgba8888
            ? source.Copy()
            : source.Copy(SKColorType.Rgba8888);

        if (rgba is null)
        {
            throw new InvalidDataException("The source image cannot be converted to RGBA pixels.");
        }

        byte[] pixels = GC.AllocateUninitializedArray<byte>(
            checked(rgba.Width * rgba.Height * RgbaBytesPerPixel));
        Dlss5PixelBuffers.CopyBitmapPixels(rgba, pixels);
        if (source.AlphaType == SKAlphaType.Premul)
        {
            Dlss5PixelBuffers.UnpremultiplyRgba(pixels);
        }

        return new Dlss5PixelBuffers(rgba.Width, rgba.Height, pixels);
    }

    public SKBitmap CreateBitmap()
    {
        SKBitmap bitmap = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        Dlss5PixelBuffers.CopyPixelsToBitmap(OutputRgba, bitmap);
        return bitmap;
    }

    public void RestoreSourceAlpha()
    {
        for (int offset = AlphaChannelOffset; offset < InputRgba.Length; offset += RgbaBytesPerPixel)
        {
            OutputRgba[offset] = InputRgba[offset];
        }
    }

    private static void CopyBitmapPixels(SKBitmap bitmap, byte[] destination)
    {
        int rowPixelBytes = checked(bitmap.Width * RgbaBytesPerPixel);
        if (bitmap.RowBytes == rowPixelBytes)
        {
            Marshal.Copy(bitmap.GetPixels(), destination, 0, destination.Length);
            return;
        }

        IntPtr pixels = bitmap.GetPixels();
        for (int row = 0; row < bitmap.Height; row++)
        {
            Marshal.Copy(
                IntPtr.Add(pixels, checked(row * bitmap.RowBytes)),
                destination,
                checked(row * rowPixelBytes),
                rowPixelBytes);
        }
    }

    private static void CopyPixelsToBitmap(byte[] source, SKBitmap bitmap)
    {
        int rowPixelBytes = checked(bitmap.Width * RgbaBytesPerPixel);
        if (bitmap.RowBytes == rowPixelBytes)
        {
            Marshal.Copy(source, 0, bitmap.GetPixels(), source.Length);
            return;
        }

        IntPtr pixels = bitmap.GetPixels();
        for (int row = 0; row < bitmap.Height; row++)
        {
            Marshal.Copy(
                source,
                checked(row * rowPixelBytes),
                IntPtr.Add(pixels, checked(row * bitmap.RowBytes)),
                rowPixelBytes);
        }
    }

    private static void UnpremultiplyRgba(Span<byte> pixels)
    {
        for (int offset = 0; offset < pixels.Length; offset += RgbaBytesPerPixel)
        {
            int alpha = pixels[offset + AlphaChannelOffset];
            if (alpha == 0)
            {
                pixels[offset + RedChannelOffset] = 0;
                pixels[offset + GreenChannelOffset] = 0;
                pixels[offset + BlueChannelOffset] = 0;
                continue;
            }

            if (alpha == byte.MaxValue)
            {
                continue;
            }

            pixels[offset + RedChannelOffset] = Dlss5PixelBuffers.UnpremultiplyChannel(
                pixels[offset + RedChannelOffset],
                alpha);
            pixels[offset + GreenChannelOffset] = Dlss5PixelBuffers.UnpremultiplyChannel(
                pixels[offset + GreenChannelOffset],
                alpha);
            pixels[offset + BlueChannelOffset] = Dlss5PixelBuffers.UnpremultiplyChannel(
                pixels[offset + BlueChannelOffset],
                alpha);
        }
    }

    private static byte UnpremultiplyChannel(byte channel, int alpha)
    {
        return (byte)Math.Min(
            byte.MaxValue,
            (channel * byte.MaxValue + alpha / IntegerRoundingDivisor) / alpha);
    }
}
