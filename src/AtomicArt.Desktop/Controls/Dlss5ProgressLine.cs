using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

public sealed class Dlss5ProgressLine : Control
{
    public IBrush? BaseBrush
    {
        get => GetValue(BaseBrushProperty);
        set => SetValue(BaseBrushProperty, value);
    }
    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly StyledProperty<IBrush?> BaseBrushProperty =
        AvaloniaProperty.Register<Dlss5ProgressLine, IBrush?>(nameof(BaseBrush));
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<Dlss5ProgressLine, IBrush?>(nameof(HighlightBrush));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<Dlss5ProgressLine, bool>(nameof(IsActive));

    private const int FrameIntervalMilliseconds = 16;
    private const int CycleDurationMilliseconds = 1050;
    private const double MinimumHighlightWidth = 32d;
    private const double MaximumHighlightWidth = 180d;
    private const double HighlightWidthRatio = 0.24d;

    private readonly PresentationAwareAnimationClock _animationClock;
    private TimeSpan _renderElapsed;

    static Dlss5ProgressLine()
    {
        AffectsRender<Dlss5ProgressLine>(BaseBrushProperty, HighlightBrushProperty, IsActiveProperty);
    }

    public Dlss5ProgressLine()
    {
        _animationClock = new PresentationAwareAnimationClock(
            FrameIntervalMilliseconds,
            InvalidateVisual);
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if ((Bounds.Width <= 0d) || (Bounds.Height <= 0d))
        {
            return;
        }

        IBrush baseBrush = BaseBrush ?? Brushes.LimeGreen;
        IBrush highlightBrush = HighlightBrush ?? Brushes.White;
        Rect bounds = new(Bounds.Size);
        context.DrawRectangle(baseBrush, null, bounds);

        double highlightWidth = Math.Clamp(
            Bounds.Width * HighlightWidthRatio,
            MinimumHighlightWidth,
            MaximumHighlightWidth);
        TimeSpan elapsed = IsActive ? _animationClock.Elapsed : _renderElapsed;
        double cycle = (elapsed.TotalMilliseconds % CycleDurationMilliseconds)
            / CycleDurationMilliseconds;
        double highlightLeft = -highlightWidth + ((Bounds.Width + highlightWidth) * cycle);
        double visibleLeft = Math.Max(0d, highlightLeft);
        double visibleRight = Math.Min(Bounds.Width, highlightLeft + highlightWidth);

        if (visibleRight > visibleLeft)
        {
            context.DrawRectangle(
                highlightBrush,
                null,
                new Rect(visibleLeft, 0d, visibleRight - visibleLeft, Bounds.Height));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animationClock.Attach(this);
        _animationClock.SetActive(IsActive);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationClock.Detach();

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            if (!IsActive)
            {
                _renderElapsed = _animationClock.Elapsed;
            }

            _animationClock.SetActive(IsActive);
            if (IsActive)
            {
                _renderElapsed = TimeSpan.Zero;
            }
        }
    }
}
