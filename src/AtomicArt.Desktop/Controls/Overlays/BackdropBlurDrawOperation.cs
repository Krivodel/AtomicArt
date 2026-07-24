using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

using SkiaSharp;

namespace AtomicArt.Desktop.Controls.Overlays;

internal sealed class BackdropBlurDrawOperation : ICustomDrawOperation
{
    public Rect Bounds { get; }

    private readonly int _captureRevision;
    private readonly bool _isDynamic;
    private readonly float _sigma;
    private SKImage? _backgroundSnapshot;

    internal BackdropBlurDrawOperation(
        Rect bounds,
        float sigma,
        bool isDynamic,
        int captureRevision)
    {
        Bounds = bounds;
        _sigma = sigma;
        _isDynamic = isDynamic;
        _captureRevision = captureRevision;
    }

    public bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public bool Equals(ICustomDrawOperation? other)
    {
        if (_isDynamic)
        {
            return false;
        }

        return other is BackdropBlurDrawOperation operation
            && !operation._isDynamic
            && operation.Bounds == Bounds
            && operation._sigma == _sigma
            && operation._captureRevision == _captureRevision;
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
        SKSurface? surface = lease.SkSurface;
        if (surface is null)
        {
            return;
        }

        SKCanvas canvas = lease.SkCanvas;
        if (!canvas.TotalMatrix.TryInvert(out SKMatrix invertedTransform))
        {
            return;
        }

        if (_isDynamic || (_backgroundSnapshot is null))
        {
            _backgroundSnapshot?.Dispose();
            _backgroundSnapshot = surface.Snapshot();
        }

        SKImage? backgroundSnapshot = _backgroundSnapshot;
        if (backgroundSnapshot is null)
        {
            return;
        }

        using SKShader backdropShader = SKShader.CreateImage(
            backgroundSnapshot,
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            invertedTransform);
        SKRect drawBounds = ToSkRect(Bounds);
        using SKImageFilter blurFilter =
            BackdropBlurImageFilterFactory.Create(
                backdropShader,
                _sigma,
                drawBounds);
        using SKPaint blurPaint = new()
        {
            IsAntialias = false,
            ImageFilter = blurFilter
        };

        int saveCount = canvas.Save();

        try
        {
            canvas.ClipRect(
                drawBounds,
                SKClipOperation.Intersect,
                antialias: false);
            canvas.DrawRect(drawBounds, blurPaint);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    public void Dispose()
    {
        _backgroundSnapshot?.Dispose();
        _backgroundSnapshot = null;
    }

    private static SKRect ToSkRect(Rect bounds)
    {
        return new SKRect(
            (float)bounds.X,
            (float)bounds.Y,
            (float)bounds.Right,
            (float)bounds.Bottom);
    }
}
