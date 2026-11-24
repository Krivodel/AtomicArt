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
    private readonly bool _useSnapshot;
    private readonly float _sigma;
    private SKImage? _backgroundSnapshot;
    private SKImage? _blurredSnapshot;

    internal BackdropBlurDrawOperation(
        Rect bounds,
        float sigma,
        bool isDynamic,
        int captureRevision,
        bool useSnapshot = false)
    {
        Bounds = bounds;
        _sigma = sigma;
        _isDynamic = isDynamic;
        _captureRevision = captureRevision;
        _useSnapshot = useSnapshot && !isDynamic;
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
                    && operation._useSnapshot == _useSnapshot
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
        SKRect drawBounds = BackdropBlurDrawOperation.ToSkRect(Bounds);

        if (_blurredSnapshot is { } cached)
        {
            BackdropBlurDrawOperation.DrawSnapshot(canvas, cached, drawBounds);

            return;
        }

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

        if (_useSnapshot)
        {
            _blurredSnapshot = BackdropBlurDrawOperation.CaptureBlurredSnapshot(lease.GrContext, blurPaint, drawBounds);

            if (_blurredSnapshot is { } blurred)
            {
                _backgroundSnapshot?.Dispose();
                _backgroundSnapshot = null;
                BackdropBlurDrawOperation.DrawSnapshot(canvas, blurred, drawBounds);

                return;
            }
        }

        BackdropBlurDrawOperation.DrawBackdrop(canvas, blurPaint, drawBounds);
    }

    public void Dispose()
    {
        _backgroundSnapshot?.Dispose();
        _backgroundSnapshot = null;
        _blurredSnapshot?.Dispose();
        _blurredSnapshot = null;
    }

    private static SKImage? CaptureBlurredSnapshot(GRContext? context, SKPaint paint, SKRect bounds)
    {
        const float SnapshotScale = 0.25f;
        SKImageInfo size = new(
            Math.Max(1, (int)Math.Ceiling(bounds.Width * SnapshotScale)),
            Math.Max(1, (int)Math.Ceiling(bounds.Height * SnapshotScale)));
        using SKSurface? snapshot = context is null
            ? SKSurface.Create(size)
            : SKSurface.Create(context, budgeted: true, size);

        if (snapshot is null)
        {
            return null;
        }

        snapshot.Canvas.Scale(size.Width / bounds.Width, size.Height / bounds.Height);
        BackdropBlurDrawOperation.DrawBackdrop(snapshot.Canvas, paint, bounds);

        return snapshot.Snapshot();
    }

    private static void DrawSnapshot(SKCanvas canvas, SKImage snapshot, SKRect bounds)
    {
        canvas.DrawImage(snapshot, bounds, new SKSamplingOptions(SKFilterMode.Linear));
    }

    private static void DrawBackdrop(SKCanvas canvas, SKPaint paint, SKRect drawBounds)
    {
        int saveCount = canvas.Save();

        try
        {
            canvas.ClipRect(
                drawBounds,
                SKClipOperation.Intersect,
                antialias: false);
            canvas.DrawRect(drawBounds, paint);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
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
