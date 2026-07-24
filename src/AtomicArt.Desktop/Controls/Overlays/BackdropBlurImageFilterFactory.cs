using SkiaSharp;

namespace AtomicArt.Desktop.Controls.Overlays;

internal static class BackdropBlurImageFilterFactory
{
    private const float DownsampleScale = 0.25f;
    private const float MinimumDownsampledSigma = 12f;
    private const float UpsampleScale = 1f / DownsampleScale;

    private static readonly SKSamplingOptions LinearSampling =
        new(SKFilterMode.Linear, SKMipmapMode.None);

    internal static SKImageFilter Create(
        SKShader backdropShader,
        float sigma,
        SKRect drawBounds)
    {
        SKRect sourceBounds = drawBounds;
        float sourcePadding = sigma * 3f;
        sourceBounds.Inflate(sourcePadding, sourcePadding);
        using SKImageFilter sourceFilter = SKImageFilter.CreateShader(
            backdropShader,
            dither: false,
            sourceBounds);

        if (!UsesDownsampling(sigma))
        {
            return SKImageFilter.CreateBlur(
                sigma,
                sigma,
                SKShaderTileMode.Clamp,
                sourceFilter);
        }

        return CreateDownsampled(sourceFilter, sigma);
    }

    internal static bool UsesDownsampling(float sigma)
    {
        return sigma >= MinimumDownsampledSigma;
    }

    private static SKImageFilter CreateDownsampled(
        SKImageFilter sourceFilter,
        float sigma)
    {
        SKMatrix downsampleMatrix =
            SKMatrix.CreateScale(DownsampleScale, DownsampleScale);
        SKMatrix upsampleMatrix =
            SKMatrix.CreateScale(UpsampleScale, UpsampleScale);
        using SKImageFilter downsampleFilter = SKImageFilter.CreateMatrix(
            in downsampleMatrix,
            LinearSampling,
            sourceFilter);
        using SKImageFilter blurFilter = SKImageFilter.CreateBlur(
            sigma * DownsampleScale,
            sigma * DownsampleScale,
            SKShaderTileMode.Clamp,
            downsampleFilter);

        return SKImageFilter.CreateMatrix(
            in upsampleMatrix,
            LinearSampling,
            blurFilter);
    }
}
