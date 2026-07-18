using SkiaSharp;

namespace AtomicArt.Desktop.Services.Generation;

internal static class AttachedImagePreparationPlanner
{
    public const int MaximumWebpDimension = 16383;

    private const int EncodingProbeMaximumDimension = 2048;
    private const int EncodingProbeActivationPixelMultiplier = 8;
    private const double ResizeSafetyFactor = 0.92d;

    public static bool ShouldUseEncodingProbe(AttachedImageCodecInfo imageInfo)
    {
        ArgumentNullException.ThrowIfNull(imageInfo);

        long sourcePixels = (long)imageInfo.Width * imageInfo.Height;
        long maximumProbePixels =
            (long)EncodingProbeMaximumDimension
            * EncodingProbeMaximumDimension;

        return sourcePixels
               > maximumProbePixels * EncodingProbeActivationPixelMultiplier;
    }

    public static SKSizeI CalculateEncodingProbeSize(AttachedImageCodecInfo imageInfo)
    {
        return CalculateInitialWorkingSize(
            imageInfo,
            EncodingProbeMaximumDimension);
    }

    public static SKSizeI CalculateInitialWorkingSize(
        AttachedImageCodecInfo imageInfo,
        int maximumDimension)
    {
        ArgumentNullException.ThrowIfNull(imageInfo);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDimension, 1);

        double scale = Math.Min(
            1d,
            Math.Min(
                (double)maximumDimension / imageInfo.Width,
                (double)maximumDimension / imageInfo.Height));

        if (scale >= 1d)
        {
            return new SKSizeI(imageInfo.Width, imageInfo.Height);
        }

        return new SKSizeI(
            Math.Max(1, (int)Math.Floor(imageInfo.Width * scale)),
            Math.Max(1, (int)Math.Floor(imageInfo.Height * scale)));
    }

    public static long EstimateEncodedBytes(
        SKSizeI targetSize,
        SKSizeI probeSize,
        long probeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSize.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(probeSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(probeSize.Height);
        ArgumentOutOfRangeException.ThrowIfLessThan(probeBytes, 1);

        long targetPixels = (long)targetSize.Width * targetSize.Height;
        long probePixels = (long)probeSize.Width * probeSize.Height;
        double estimatedBytes = (double)probeBytes * targetPixels / probePixels;

        return estimatedBytes >= long.MaxValue
            ? long.MaxValue
            : Math.Max(1L, (long)Math.Ceiling(estimatedBytes));
    }

    public static SKSizeI CalculateReducedSize(
        int width,
        int height,
        long encodedBytes,
        long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(encodedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);

        double scale = CalculateEncodedSizeScale(encodedBytes, maxBytes);
        int targetWidth = Math.Max(1, (int)Math.Floor(width * scale));
        int targetHeight = Math.Max(1, (int)Math.Floor(height * scale));

        return new SKSizeI(targetWidth, targetHeight);
    }

    private static double CalculateEncodedSizeScale(long encodedBytes, long maxBytes)
    {
        double ratio = (double)maxBytes / encodedBytes;

        return Math.Min(
            ResizeSafetyFactor,
            Math.Sqrt(ratio) * ResizeSafetyFactor);
    }
}
