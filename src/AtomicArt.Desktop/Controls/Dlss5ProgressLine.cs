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
    private const int CycleDurationMilliseconds = 2250;
    private const int MaximumCellCount = 32;
    private const double CellPitch = 15d;
    private const double CellGap = 3d;
    private const double CellHeight = 5d;
    private const double HorizontalInset = 7d;
    private const double ActiveCellSpan = 4d;
    private const double TailOpacity = 0.35d;

    private readonly PresentationAwareAnimationClock _animationClock;
    private TimeSpan _renderElapsed;

    static Dlss5ProgressLine()
    {
        AffectsRender<Dlss5ProgressLine>(
            BaseBrushProperty,
            HighlightBrushProperty,
            IsActiveProperty);
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
        ArgumentNullException.ThrowIfNull(context);

        base.Render(context);

        if (!double.IsFinite(Bounds.Width)
            || !double.IsFinite(Bounds.Height)
            || (Bounds.Width <= 0d)
            || (Bounds.Height <= 0d))
        {
            return;
        }

        IBrush baseBrush = BaseBrush ?? Brushes.LimeGreen;
        IBrush highlightBrush = HighlightBrush ?? Brushes.White;

        double usableWidth = Math.Max(0d, Bounds.Width - (2d * HorizontalInset));

        if (usableWidth <= 0d)
        {
            return;
        }

        int cellCount = (int)Math.Clamp(
            Math.Floor(usableWidth / CellPitch),
            1d,
            MaximumCellCount);
        double cellWidth = (usableWidth - ((cellCount - 1) * CellGap)) / cellCount;
        double cellHeight = Math.Min(CellHeight, Bounds.Height);
        double cellTop = (Bounds.Height - cellHeight) / 2d;
        TimeSpan elapsed = IsActive ? _animationClock.Elapsed : _renderElapsed;
        double headPosition = ((elapsed.TotalMilliseconds % CycleDurationMilliseconds)
            / CycleDurationMilliseconds) * cellCount;

        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            Rect cell = new(
                HorizontalInset + (cellIndex * (cellWidth + CellGap)),
                cellTop,
                cellWidth,
                cellHeight);
            context.DrawRectangle(baseBrush, null, cell);

            double distanceBehindHead = (headPosition - cellIndex + cellCount) % cellCount;

            if (distanceBehindHead >= ActiveCellSpan)
            {
                continue;
            }

            double opacity = 1d - ((distanceBehindHead / ActiveCellSpan) * (1d - TailOpacity));

            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(highlightBrush, null, cell);
            }
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
