using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

public sealed class Dlss5SourceLoadingControl : Control
{
    public IBrush? PrimaryBrush
    {
        get => GetValue(PrimaryBrushProperty);
        set => SetValue(PrimaryBrushProperty, value);
    }
    public IBrush? SecondaryBrush
    {
        get => GetValue(SecondaryBrushProperty);
        set => SetValue(SecondaryBrushProperty, value);
    }
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly StyledProperty<IBrush?> PrimaryBrushProperty =
        AvaloniaProperty.Register<Dlss5SourceLoadingControl, IBrush?>(nameof(PrimaryBrush));
    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<Dlss5SourceLoadingControl, IBrush?>(nameof(SecondaryBrush));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<Dlss5SourceLoadingControl, bool>(nameof(IsActive));

    private const int FrameIntervalMilliseconds = 16;
    private const int CycleDurationMilliseconds = 1500;
    private const double FrameWidthRatio = 0.82d;
    private const double FrameHeightRatio = 0.68d;
    private const double OutlineThickness = 1.5d;
    private const double ScanThickness = 2d;
    private const double MarkerRadius = 2.5d;

    private readonly PresentationAwareAnimationClock _animationClock;

    static Dlss5SourceLoadingControl()
    {
        AffectsRender<Dlss5SourceLoadingControl>(
            PrimaryBrushProperty,
            SecondaryBrushProperty,
            IsActiveProperty);
    }

    public Dlss5SourceLoadingControl()
    {
        _animationClock = new PresentationAwareAnimationClock(
            FrameIntervalMilliseconds,
            InvalidateVisual);
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsActive || (Bounds.Width <= 0d) || (Bounds.Height <= 0d))
        {
            return;
        }

        IBrush primaryBrush = PrimaryBrush ?? Brushes.Cyan;
        IBrush secondaryBrush = SecondaryBrush ?? Brushes.MediumPurple;
        double frameWidth = Bounds.Width * FrameWidthRatio;
        double frameHeight = Bounds.Height * FrameHeightRatio;
        Rect frame = new(
            (Bounds.Width - frameWidth) / 2d,
            (Bounds.Height - frameHeight) / 2d,
            frameWidth,
            frameHeight);
        Pen primaryPen = new(primaryBrush, OutlineThickness);
        Pen secondaryPen = new(secondaryBrush, OutlineThickness);
        context.DrawRectangle(null, primaryPen, frame);
        context.DrawLine(
            secondaryPen,
            new Point(frame.Left, frame.Bottom),
            new Point(frame.Right, frame.Bottom));
        context.DrawLine(
            secondaryPen,
            new Point(frame.Right, frame.Top),
            new Point(frame.Right, frame.Bottom));

        double cycle = (_animationClock.Elapsed.TotalMilliseconds % CycleDurationMilliseconds)
            / CycleDurationMilliseconds;
        double scanY = frame.Top + (frame.Height * cycle);
        Pen scanPen = new(primaryBrush, ScanThickness);
        context.DrawLine(
            scanPen,
            new Point(frame.Left, scanY),
            new Point(frame.Right, scanY));

        double markerX = frame.Left
            + (frame.Width * (0.5d + (0.42d * Math.Sin(cycle * Math.PI * 2d))));
        context.DrawEllipse(
            secondaryBrush,
            null,
            new Point(markerX, scanY),
            MarkerRadius,
            MarkerRadius);

        double imageBaseline = frame.Bottom - (frame.Height * 0.2d);
        context.DrawLine(
            secondaryPen,
            new Point(frame.Left + (frame.Width * 0.12d), imageBaseline),
            new Point(frame.Left + (frame.Width * 0.38d), frame.Top + (frame.Height * 0.48d)));
        context.DrawLine(
            secondaryPen,
            new Point(frame.Left + (frame.Width * 0.38d), frame.Top + (frame.Height * 0.48d)),
            new Point(frame.Left + (frame.Width * 0.58d), imageBaseline));
        context.DrawLine(
            secondaryPen,
            new Point(frame.Left + (frame.Width * 0.58d), imageBaseline),
            new Point(frame.Left + (frame.Width * 0.73d), frame.Top + (frame.Height * 0.61d)));
        context.DrawLine(
            secondaryPen,
            new Point(frame.Left + (frame.Width * 0.73d), frame.Top + (frame.Height * 0.61d)),
            new Point(frame.Right - (frame.Width * 0.08d), imageBaseline));
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
            _animationClock.SetActive(IsActive);
        }
    }
}
