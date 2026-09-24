using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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
        _content.Bounds,
        _widthRatio,
        _heightRatio,
        _origin);
    internal BoxShadows BoxShadows => _boxShadows;
    internal double WidthRatio => _widthRatio;
    internal double HeightRatio => _heightRatio;

    private const double ShadowMeasurementSize = 1d;

    private readonly Control _content;
    private BoxShadows _boxShadows;
    private ContextMenuRevealOrigin _origin;
    private bool _isRevealActive;
    private double _widthRatio = InitialWidthRatio;
    private double _heightRatio = InitialHeightRatio;

    internal ContextMenuRevealHost(Control content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        Child = content;
        Opacity = 0d;
    }

    public override void Render(DrawingContext context)
    {
        if (_content is MenuFlyoutPresenter presenter)
        {
            Rect contentBounds = _isRevealActive
                ? RevealBounds
                : presenter.Bounds;
            Rect shadowBounds = CalculateShadowBounds(
                contentBounds,
                presenter.BorderThickness);
            RoundedRect roundedShadowBounds = new(
                shadowBounds,
                presenter.CornerRadius);
            context.DrawRectangle(
                null,
                null,
                roundedShadowBounds,
                _boxShadows);
        }

        base.Render(context);
    }

    public void Dispose()
    {
        _isRevealActive = false;
        Opacity = 1d;
        _content.Clip = null;
        _content.Opacity = 1d;
        _content.IsHitTestVisible = true;
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

    internal Control DetachContent()
    {
        Child = null;
        _content.Clip = null;
        _content.Opacity = 1d;
        _content.IsHitTestVisible = true;

        return _content;
    }

    internal void BeginReveal(ContextMenuRevealOrigin origin)
    {
        _isRevealActive = true;
        _origin = origin;
        Opacity = 0d;
        _content.Opacity = 1d;
        ApplyOpeningProgress(0d);
    }

    internal void AnimateOpen(
        UiAnimationScheduler animationScheduler,
        ContextMenuRevealOrigin origin,
        Action completed)
    {
        ArgumentNullException.ThrowIfNull(animationScheduler);
        ArgumentNullException.ThrowIfNull(completed);

        BeginReveal(origin);
        _ = animationScheduler.AnimateValueAsync(
            this,
            0d,
            1d,
            OpeningDurationMilliseconds,
            0,
            MotionEasing.Linear,
            ApplyOpeningProgress,
            completed);
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
        UpdateContentClip();
        InvalidateVisual();
    }

    internal void CompleteReveal()
    {
        _widthRatio = 1d;
        _heightRatio = 1d;
        _isRevealActive = false;
        Opacity = 1d;
        _content.Clip = null;
        _content.Opacity = 1d;
        _content.IsHitTestVisible = true;
        InvalidateVisual();
    }

    internal void SetBoxShadows(BoxShadows boxShadows)
    {
        _boxShadows = boxShadows;

        if (_content is MenuFlyoutPresenter presenter)
        {
            Padding = CalculateShadowPadding(
                boxShadows,
                presenter.BorderThickness);
        }

        InvalidateVisual();
    }

    private void UpdateContentClip()
    {
        if (!_isRevealActive)
        {
            _content.Clip = null;
            return;
        }

        Rect revealBounds = RevealBounds;
        Rect contentClipBounds = new(
            revealBounds.X - _content.Bounds.X,
            revealBounds.Y - _content.Bounds.Y,
            revealBounds.Width,
            revealBounds.Height);
        _content.Clip = new RectangleGeometry(contentClipBounds);
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
