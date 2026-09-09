using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls;

public sealed class LoadingSpinnerControl : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<LoadingSpinnerControl, bool>(nameof(IsActive));

    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<LoadingSpinnerControl, IBrush?>(nameof(Brush));

    private const double SweepAngle = 280d;
    private const double RotationDegreesPerTick = 8d;

    private readonly DispatcherTimer _timer;
    private double _rotationAngle;

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public LoadingSpinnerControl()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16d)
        };
        _timer.Tick += OnTimerTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimerState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            UpdateTimerState();
            InvalidateVisual();
        }
        else if (change.Property == BrushProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsActive || Bounds.Width <= 0d || Bounds.Height <= 0d)
        {
            return;
        }

        double size = Math.Min(Bounds.Width, Bounds.Height);
        double strokeThickness = Math.Max(1d, size * 0.14d);
        double radius = Math.Max(0d, (size - strokeThickness) / 2d);
        Point center = new(Bounds.Width / 2d, Bounds.Height / 2d);
        double startAngle = (_rotationAngle - 90d) * Math.PI / 180d;
        double endAngle = (_rotationAngle - 90d + SweepAngle) * Math.PI / 180d;
        Point start = new(
            center.X + Math.Cos(startAngle) * radius,
            center.Y + Math.Sin(startAngle) * radius);
        Point end = new(
            center.X + Math.Cos(endAngle) * radius,
            center.Y + Math.Sin(endAngle) * radius);

        StreamGeometry geometry = new();
        using (StreamGeometryContext geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(start, isFilled: false);
            geometryContext.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0d,
                isLargeArc: SweepAngle > 180d,
                SweepDirection.Clockwise);
        }

        IBrush brush = Brush ?? Brushes.White;
        Pen pen = new(brush, strokeThickness);
        context.DrawGeometry(null, pen, geometry);
        context.DrawEllipse(brush, null, start, strokeThickness / 2d, strokeThickness / 2d);
        context.DrawEllipse(brush, null, end, strokeThickness / 2d, strokeThickness / 2d);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _rotationAngle = (_rotationAngle + RotationDegreesPerTick) % 360d;
        InvalidateVisual();
    }

    private void UpdateTimerState()
    {
        if (IsActive && VisualRoot is not null)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }
}
