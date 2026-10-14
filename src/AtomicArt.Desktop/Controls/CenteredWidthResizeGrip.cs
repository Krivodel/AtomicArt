using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AtomicArt.Desktop.Controls;

public sealed class CenteredWidthResizeGrip : GridSplitter
{
    public Control? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }
    public Control? ResizeContainer
    {
        get => GetValue(ResizeContainerProperty);
        set => SetValue(ResizeContainerProperty, value);
    }
    public double MinimumTargetWidth
    {
        get => GetValue(MinimumTargetWidthProperty);
        set => SetValue(MinimumTargetWidthProperty, value);
    }

    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<CenteredWidthResizeGrip, Control?>(nameof(Target));
    public static readonly StyledProperty<Control?> ResizeContainerProperty =
        AvaloniaProperty.Register<CenteredWidthResizeGrip, Control?>(nameof(ResizeContainer));
    public static readonly StyledProperty<double> MinimumTargetWidthProperty =
        AvaloniaProperty.Register<CenteredWidthResizeGrip, double>(nameof(MinimumTargetWidth));

    protected override Type StyleKeyOverride => typeof(GridSplitter);

    private const double CenteredWidthMultiplier = 2d;

    private double WidthChangeMultiplier => HorizontalAlignment == HorizontalAlignment.Left
        ? -CenteredWidthMultiplier
        : CenteredWidthMultiplier;

    private Point? _dragStartPosition;
    private Point _dragStartLocalPosition;
    private double _dragStartWidth;

    protected override void OnDragStarted(VectorEventArgs e)
    {
        _dragStartPosition = null;

        if (Target is Control target && ResizeContainer is Control container)
        {
            _dragStartLocalPosition = new Point(e.Vector.X, e.Vector.Y);
            _dragStartPosition = this.TranslatePoint(_dragStartLocalPosition, container);
            _dragStartWidth = target.Bounds.Width;
        }

        e.Handled = true;
    }

    protected override void OnDragDelta(VectorEventArgs e)
    {
        if (_dragStartPosition is Point startPosition
            && ResizeContainer is Control container
            && this.TranslatePoint(_dragStartLocalPosition + e.Vector, container) is Point position)
        {
            SetRequestedWidth(_dragStartWidth + (position.X - startPosition.X) * WidthChangeMultiplier);
        }

        e.Handled = true;
    }

    protected override void OnDragCompleted(VectorEventArgs e)
    {
        _dragStartPosition = null;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right)
        {
            ResizeWidth(e.Key == Key.Right ? KeyboardIncrement : -KeyboardIncrement);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void ResizeWidth(double horizontalChange)
    {
        if (Target is Control target)
        {
            SetRequestedWidth(target.Bounds.Width + horizontalChange * WidthChangeMultiplier);
        }
    }

    private void SetRequestedWidth(double requestedWidth)
    {
        if (Target is not Control target || ResizeContainer is not Control container)
        {
            return;
        }

        double maximumWidth = Math.Max(0d, container.Bounds.Width - target.Margin.Left - target.Margin.Right);
        double minimumWidth = Math.Max(0d, MinimumTargetWidth);

        if (maximumWidth <= minimumWidth)
        {
            return;
        }

        target.SetCurrentValue(MaxWidthProperty, Math.Clamp(requestedWidth, minimumWidth, maximumWidth));
    }
}
