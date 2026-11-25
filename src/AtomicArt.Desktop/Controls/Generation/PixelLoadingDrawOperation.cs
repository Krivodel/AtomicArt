using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

using SkiaSharp;

namespace AtomicArt.Desktop.Controls.Generation;

internal sealed class PixelLoadingDrawOperation : ICustomDrawOperation
{
    public Rect Bounds { get; }

    private const double MinimumOpacity = 0.1d;
    private const double OpacityRange = 0.85d;
    private const double FlickerDurationSeconds = 1.8d;
    private const double CompletionStaggerRange = 0.42d;

    private readonly int _gridSize;
    private readonly double _pixelGap;
    private readonly double _pixelCornerRadius;
    private readonly double _pixelSideLength;
    private readonly double _originX;
    private readonly double _originY;
    private readonly double _elapsedSeconds;
    private readonly double _completionProgress;
    private readonly bool _usesUniformCompletionFade;
    private readonly PixelLoadingState[] _pixels;
    private readonly bool _cacheFrames;
    private SKImage? _cachedFrame;
    private GRContext? _graphicsContext;

    public PixelLoadingDrawOperation(
        Rect bounds,
        int gridSize,
        double pixelGap,
        double pixelCornerRadius,
        double pixelSideLength,
        double originX,
        double originY,
        double elapsedSeconds,
        double completionProgress,
        bool usesUniformCompletionFade,
        PixelLoadingState[] pixels,
        bool cacheFrames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gridSize, 1);
        ArgumentNullException.ThrowIfNull(pixels);

        Bounds = bounds;
        _gridSize = gridSize;
        _pixelGap = pixelGap;
        _pixelCornerRadius = pixelCornerRadius;
        _pixelSideLength = pixelSideLength;
        _originX = originX;
        _originY = originY;
        _elapsedSeconds = elapsedSeconds;
        _completionProgress = completionProgress;
        _usesUniformCompletionFade = usesUniformCompletionFade;
        _pixels = pixels;
        _cacheFrames = cacheFrames;
    }

    public bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public bool Equals(ICustomDrawOperation? other)
    {
        return other is PixelLoadingDrawOperation operation
            && operation.Bounds == Bounds
            && operation._gridSize == _gridSize
            && operation._pixelGap == _pixelGap
            && operation._pixelCornerRadius == _pixelCornerRadius
            && operation._pixelSideLength == _pixelSideLength
            && operation._originX == _originX
            && operation._originY == _originY
            && operation._elapsedSeconds == _elapsedSeconds
            && operation._completionProgress == _completionProgress
            && operation._usesUniformCompletionFade == _usesUniformCompletionFade
            && operation._cacheFrames == _cacheFrames
            && ReferenceEquals(operation._pixels, _pixels);
    }

    public void Render(ImmediateDrawingContext context)
    {
        ISkiaSharpApiLeaseFeature? leaseFeature =
            context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            return;
        }

        using ISkiaSharpApiLease lease = leaseFeature.Lease();
        SKCanvas canvas = lease.SkCanvas;

        if (!_cacheFrames)
        {
            DrawPixels(canvas);
            return;
        }

        if (_cachedFrame is null || !ReferenceEquals(_graphicsContext, lease.GrContext))
        {
            _cachedFrame?.Dispose();
            _cachedFrame = null;
            _graphicsContext = lease.GrContext;
            SKImageInfo info = new(Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
            using SKSurface? surface = lease.GrContext is { } graphicsContext
                ? SKSurface.Create(graphicsContext, false, info)
                : SKSurface.Create(info);

            if (surface is not null)
            {
                surface.Canvas.Clear(SKColors.Transparent);
                DrawPixels(surface.Canvas);
                _cachedFrame = surface.Snapshot();
            }
        }

        if (_cachedFrame is { } frame)
        {
            canvas.DrawImage(frame, new SKRect(0f, 0f, (float)Bounds.Width, (float)Bounds.Height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), null);
        }
        else
        {
            DrawPixels(canvas);
        }
    }

    public void Dispose()
    {
        _cachedFrame?.Dispose();
        _cachedFrame = null;
        _graphicsContext = null;
    }

    private void DrawPixels(SKCanvas canvas)
    {
        using SKPaint paint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        for (int index = 0; index < _pixels.Length; index++)
        {
            PixelLoadingState pixel = _pixels[index];
            int row = index / _gridSize;
            int column = index % _gridSize;
            float pixelX = (float)(_originX + (column * (_pixelSideLength + _pixelGap)));
            float pixelY = (float)(_originY + (row * (_pixelSideLength + _pixelGap)));
            float pixelRight = pixelX + (float)_pixelSideLength;
            float pixelBottom = pixelY + (float)_pixelSideLength;
            double opacity = CalculateOpacity(pixel);

            paint.Color = pixel.Color.WithAlpha(ToAlpha(opacity));
            canvas.DrawRoundRect(
                new SKRect(pixelX, pixelY, pixelRight, pixelBottom),
                (float)_pixelCornerRadius,
                (float)_pixelCornerRadius,
                paint);
        }
    }

    private double CalculateOpacity(PixelLoadingState pixel)
    {
        double shimmer = 0.5d
            + (0.5d * Math.Sin(
                pixel.InitialPhase
                + (_elapsedSeconds * Math.Tau / FlickerDurationSeconds)));
        double opacity = MinimumOpacity + (OpacityRange * shimmer);

        if (_completionProgress <= 0d)
        {
            return opacity;
        }

        if (_usesUniformCompletionFade)
        {
            return opacity * (1d - _completionProgress);
        }

        double staggerStart = pixel.DisappearOrder * CompletionStaggerRange;
        double localProgress = Math.Clamp(
            (_completionProgress - staggerStart) / (1d - staggerStart),
            0d,
            1d);
        double smoothProgress = localProgress
            * localProgress
            * (3d - (2d * localProgress));

        return opacity * (1d - smoothProgress);
    }

    private static byte ToAlpha(double opacity)
    {
        return (byte)Math.Round(Math.Clamp(opacity, 0d, 1d) * byte.MaxValue);
    }
}
