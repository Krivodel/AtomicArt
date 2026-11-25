using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class GenerationChatBranchConnectionControl : Control
{
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<GenerationChatBranchConnectionControl, IBrush?>(nameof(Stroke));

    private readonly GeometryDrawing _drawing;
    private readonly Geometry _geometry;
    private readonly Matrix _translation;
    private readonly double _thickness;
    private IPen? _pen;

    public GenerationChatBranchConnectionControl(GeometryDrawing drawing)
    {
        _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));

        if (drawing is not { Geometry: { } geometry, Pen: { } pen })
        {
            throw new ArgumentException("A branch connection requires its geometry and pen.", nameof(drawing));
        }

        Rect bounds = geometry.GetRenderBounds(pen);
        _geometry = geometry;
        Width = bounds.Width;
        Height = bounds.Height;
        Canvas.SetLeft(this, bounds.X);
        Canvas.SetTop(this, bounds.Y);
        _translation = Matrix.CreateTranslation(-bounds.X, -bounds.Y);
        _thickness = pen.Thickness;
        _pen = drawing.Pen;
        Stroke = pen.Brush;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        using (context.PushTransform(_translation))
        {
            context.DrawGeometry(null, new Pen(Stroke, _thickness), _geometry);
        }
    }

    internal void RefreshPen()
    {
        if (!ReferenceEquals(_pen, _drawing.Pen))
        {
            _pen = _drawing.Pen;
            Stroke = _pen?.Brush;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StrokeProperty)
        {
            InvalidateVisual();
        }
    }
}
