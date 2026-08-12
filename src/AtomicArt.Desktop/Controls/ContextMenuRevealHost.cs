using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

internal sealed class ContextMenuRevealHost : Decorator, IDisposable
{
    internal const int OpeningDurationMilliseconds = 200;
    internal const int WidthRevealDurationMilliseconds = 120;
    internal const int HeightRevealDurationMilliseconds = 180;
    internal const int OpacityRevealDurationMilliseconds = 60;
    internal const double InitialWidthRatio = 0.5d;
    internal const double InitialHeightRatio = 0.3d;

    internal Rect RevealBounds => CalculateRevealBounds(
        _presenter.Bounds,
        _widthRatio,
        _heightRatio,
        _origin);
    internal RenderTargetBitmap? Snapshot => _snapshot;
    internal BoxShadows BoxShadows => _boxShadows;
    internal double WidthRatio => _widthRatio;
    internal double HeightRatio => _heightRatio;

    private const double ShadowMeasurementSize = 1d;

    private readonly MenuFlyoutPresenter _presenter;
    private RenderTargetBitmap? _snapshot;
    private BoxShadows _boxShadows;
    private ContextMenuRevealOrigin _origin;
    private double _widthRatio = InitialWidthRatio;
    private double _heightRatio = InitialHeightRatio;

    internal ContextMenuRevealHost(MenuFlyoutPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        Child = presenter;
        Opacity = 0d;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect contentBounds = _snapshot is null
            ? _presenter.Bounds
            : RevealBounds;
        Rect shadowBounds = CalculateShadowBounds(
            contentBounds,
            _presenter.BorderThickness);
        RoundedRect roundedShadowBounds = new(
            shadowBounds,
            _presenter.CornerRadius);
        context.DrawRectangle(
            null,
            null,
            roundedShadowBounds,
            _boxShadows);

        if (_snapshot is null)
        {
            return;
        }

        RoundedRect roundedContentBounds = new(
            contentBounds,
            _presenter.CornerRadius);

        using (context.PushClip(roundedContentBounds))
        {
            context.DrawImage(_snapshot, _presenter.Bounds);
        }
    }

    public void Dispose()
    {
        _snapshot?.Dispose();
        _snapshot = null;
        _presenter.Opacity = 1d;
        _presenter.IsHitTestVisible = true;
    }

    internal static Rect CalculateRevealBounds(
        Rect menuBounds,
        double widthRatio,
        double heightRatio,
        ContextMenuRevealOrigin origin)
    {
        double width = menuBounds.Width * Math.Clamp(widthRatio, 0d, 1d);
        double height = menuBounds.Height * Math.Clamp(heightRatio, 0d, 1d);
        bool revealFromRight = origin is ContextMenuRevealOrigin.TopRight
            or ContextMenuRevealOrigin.BottomRight;
        bool revealFromBottom = origin is ContextMenuRevealOrigin.BottomLeft
            or ContextMenuRevealOrigin.BottomRight;
        double x = revealFromRight
            ? menuBounds.Right - width
            : menuBounds.X;
        double y = revealFromBottom
            ? menuBounds.Bottom - height
            : menuBounds.Y;

        return new Rect(x, y, width, height);
    }

    internal static Rect CalculateShadowBounds(
        Rect contentBounds,
        Thickness borderThickness)
    {
        return contentBounds.Deflate(borderThickness);
    }

    internal MenuFlyoutPresenter DetachPresenter()
    {
        Child = null;
        _presenter.Opacity = 1d;
        _presenter.IsHitTestVisible = true;

        return _presenter;
    }

    internal void BeginReveal(
        RenderTargetBitmap snapshot,
        ContextMenuRevealOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshot?.Dispose();
        _snapshot = snapshot;
        _origin = origin;
        _presenter.Opacity = 0d;
        ApplyOpeningProgress(0d);
    }

    internal void ApplyOpeningProgress(double progress)
    {
        double elapsedMilliseconds = Math.Clamp(progress, 0d, 1d)
            * OpeningDurationMilliseconds;
        _widthRatio = InterpolateRevealRatio(
            InitialWidthRatio,
            elapsedMilliseconds,
            WidthRevealDurationMilliseconds);
        _heightRatio = InterpolateRevealRatio(
            InitialHeightRatio,
            elapsedMilliseconds,
            HeightRevealDurationMilliseconds);
        Opacity = MotionEasing.EaseOutCirc(Math.Clamp(
            elapsedMilliseconds / OpacityRevealDurationMilliseconds,
            0d,
            1d));
        InvalidateVisual();
    }

    internal void CompleteReveal()
    {
        _widthRatio = 1d;
        _heightRatio = 1d;
        Opacity = 1d;
        _presenter.Opacity = 1d;
        _presenter.IsHitTestVisible = true;
        _snapshot?.Dispose();
        _snapshot = null;
        InvalidateVisual();
    }

    internal void SetBoxShadows(BoxShadows boxShadows)
    {
        _boxShadows = boxShadows;
        Padding = CalculateShadowPadding(
            boxShadows,
            _presenter.BorderThickness);
        InvalidateVisual();
    }

    private static Thickness CalculateShadowPadding(
        BoxShadows boxShadows,
        Thickness borderThickness)
    {
        Rect contentBounds = new(
            0d,
            0d,
            borderThickness.Left
                + borderThickness.Right
                + ShadowMeasurementSize,
            borderThickness.Top
                + borderThickness.Bottom
                + ShadowMeasurementSize);
        Rect shadowSurfaceBounds = CalculateShadowBounds(
            contentBounds,
            borderThickness);
        Rect shadowBounds = boxShadows.TransformBounds(shadowSurfaceBounds);

        return new Thickness(
            Math.Max(0d, contentBounds.Left - shadowBounds.Left),
            Math.Max(0d, contentBounds.Top - shadowBounds.Top),
            Math.Max(0d, shadowBounds.Right - contentBounds.Right),
            Math.Max(0d, shadowBounds.Bottom - contentBounds.Bottom));
    }

    private static double InterpolateRevealRatio(
        double initialRatio,
        double elapsedMilliseconds,
        int durationMilliseconds)
    {
        double rawProgress = Math.Clamp(
            elapsedMilliseconds / durationMilliseconds,
            0d,
            1d);
        double easedProgress = MotionEasing.EaseOutCirc(rawProgress);

        return initialRatio + ((1d - initialRatio) * easedProgress);
    }
}
