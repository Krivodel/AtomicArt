using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class GenerationChatBranchPanel : Panel
{
    public const double NodeWidth = 220d;
    public const double NodeHeight = 160d;

    public IBrush? ConnectionBrush
    {
        get => GetValue(ConnectionBrushProperty);
        set => SetValue(ConnectionBrushProperty, value);
    }
    public IBrush? CurrentConnectionBrush
    {
        get => GetValue(CurrentConnectionBrushProperty);
        set => SetValue(CurrentConnectionBrushProperty, value);
    }
    public Guid? HoveredNodeId
    {
        get => GetValue(HoveredNodeIdProperty);
        set => SetValue(HoveredNodeIdProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ConnectionBrushProperty =
        AvaloniaProperty.Register<GenerationChatBranchPanel, IBrush?>(nameof(ConnectionBrush));
    public static readonly StyledProperty<IBrush?> CurrentConnectionBrushProperty =
        AvaloniaProperty.Register<GenerationChatBranchPanel, IBrush?>(nameof(CurrentConnectionBrush));
    public static readonly StyledProperty<Guid?> HoveredNodeIdProperty =
        AvaloniaProperty.Register<GenerationChatBranchPanel, Guid?>(nameof(HoveredNodeId));

    internal IReadOnlySet<Guid> HighlightedNodeIds => _highlightedNodes;
    internal DrawingGroup ConnectionDrawing { get; private set; } = new();

    internal event EventHandler? ConnectionsChanged;

    private const double ColumnGap = 100d;
    private const double RowGap = 28d;
    private const double ContentPadding = 24d;
    private const double ConnectionThickness = 1.5d;

    private readonly Dictionary<Control, Rect> _bounds = [];
    private readonly Dictionary<Guid, GenerationChatBranchNodeViewModel> _nodes = [];
    private readonly List<(Guid ChildId, Guid ParentId, GeometryDrawing Drawing)> _connections = [];
    private readonly HashSet<Guid> _highlightedNodes = [];
    private Pen? _neutralPen;
    private Pen? _currentPen;
    private Size _layoutSize;
    private bool _layoutInvalidated = true;

    static GenerationChatBranchPanel()
    {
        AffectsMeasure<GenerationChatBranchPanel>(ConnectionBrushProperty, CurrentConnectionBrushProperty);
        HoveredNodeIdProperty.Changed.AddClassHandler<GenerationChatBranchPanel>(OnHoveredNodeChanged);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (Control child in Children)
        {
            child.Measure(new Size(NodeWidth, NodeHeight));
        }

        if (!_layoutInvalidated && _bounds.Count == Children.Count && Children.All(child =>
            _bounds.ContainsKey(child) && child.DataContext is GenerationChatBranchNodeViewModel node
            && ReferenceEquals(_nodes.GetValueOrDefault(node.Id), node)))
        {
            return _layoutSize;
        }

        DetachNodes();
        _bounds.Clear();
        _nodes.Clear();
        _connections.Clear();
        Dictionary<Guid, Control> controls = [];

        foreach (Control child in Children)
        {
            if (child.DataContext is GenerationChatBranchNodeViewModel node)
            {
                controls.Add(node.Id, child);
                _nodes.Add(node.Id, node);
            }
        }

        if (this.IsAttachedToVisualTree())
        {
            AttachNodes();
        }

        Dictionary<Guid, List<Control>> descendants = [];
        List<Control> roots = [];

        DrawingGroup drawing = new();
        _neutralPen = new Pen(ConnectionBrush, ConnectionThickness);
        _currentPen = new Pen(CurrentConnectionBrush, ConnectionThickness);

        foreach (Control child in controls.Values)
        {
            if (child.DataContext is GenerationChatBranchNodeViewModel { ParentId: Guid parent } && controls.ContainsKey(parent))
            {
                if (!descendants.TryGetValue(parent, out List<Control>? siblings))
                {
                    siblings = new List<Control>();
                    descendants.Add(parent, siblings);
                }

                siblings.Add(child);
            }
            else
            {
                roots.Add(child);
            }
        }

        List<(Control Control, int Depth)> ordered = [];
        Stack<(Control Control, int Depth)> pending = new Stack<(Control Control, int Depth)>(
            roots.AsEnumerable().Reverse().Select(child => (child, 0)));

        while (pending.TryPop(out (Control Control, int Depth) entry))
        {
            ordered.Add(entry);

            if (entry.Control.DataContext is GenerationChatBranchNodeViewModel node
                && descendants.TryGetValue(node.Id, out List<Control>? children))
            {
                for (int index = children.Count - 1; index >= 0; index--)
                {
                    pending.Push((children[index], entry.Depth + 1));
                }
            }
        }

        Dictionary<Control, double> centers = [];
        double leafTop = ContentPadding;

        foreach ((Control child, int _) in ordered)
        {
            if (child.DataContext is GenerationChatBranchNodeViewModel node && !descendants.ContainsKey(node.Id))
            {
                centers[child] = leafTop + NodeHeight / 2d;
                leafTop += NodeHeight + RowGap;
            }
        }

        for (int index = ordered.Count - 1; index >= 0; index--)
        {
            (Control child, int depth) = ordered[index];

            if (child.DataContext is GenerationChatBranchNodeViewModel node
                && descendants.TryGetValue(node.Id, out List<Control>? children))
            {
                centers[child] = (centers[children[0]] + centers[children[^1]]) / 2d;
            }

            _bounds[child] = new Rect(ContentPadding + depth * (NodeWidth + ColumnGap),
                centers[child] - NodeHeight / 2d, NodeWidth, NodeHeight);
        }

        foreach (Control child in controls.Values)
        {
            if (child.DataContext is GenerationChatBranchNodeViewModel { ParentId: Guid parent } node
                && controls.TryGetValue(parent, out Control? owner))
            {
                Rect from = _bounds[owner];
                Rect to = _bounds[child];
                double middle = (from.Right + to.Left) / 2d;
                StreamGeometry geometry = new();

                using (StreamGeometryContext geometryContext = geometry.Open())
                {
                    geometryContext.BeginFigure(from.Center.WithX(from.Right), false);
                    geometryContext.CubicBezierTo(new Point(middle, from.Center.Y), new Point(middle, to.Center.Y), to.Center.WithX(to.Left));
                    geometryContext.EndFigure(false);
                }

                GeometryDrawing connection = new()
                {
                    Geometry = geometry,
                    Pen = _neutralPen
                };
                drawing.Children.Add(connection);
                _connections.Add((node.Id, parent, connection));
            }
        }

        _layoutSize = _bounds.Count == 0 ? default : new Size(_bounds.Values.Max(rect => rect.Right) + ContentPadding,
            _bounds.Values.Max(rect => rect.Bottom) + ContentPadding);
        _layoutInvalidated = false;
        ConnectionDrawing = drawing;
        UpdateHighlightedRoute();

        return _layoutSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (Control child in Children)
        {
            child.Arrange(_bounds.GetValueOrDefault(child));
        }

        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ConnectionBrushProperty || change.Property == CurrentConnectionBrushProperty)
        {
            _layoutInvalidated = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachNodes();
        _layoutInvalidated = true;
        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachNodes();
        base.OnDetachedFromVisualTree(e);
    }

    private static void OnHoveredNodeChanged(GenerationChatBranchPanel panel, AvaloniaPropertyChangedEventArgs e)
    {
        panel.UpdateHighlightedRoute();
    }

    private void UpdateHighlightedRoute()
    {
        _highlightedNodes.Clear();
        IEnumerable<GenerationChatBranchNodeViewModel> targets = HoveredNodeId is Guid id && _nodes.TryGetValue(id, out GenerationChatBranchNodeViewModel? hovered)
            ? new GenerationChatBranchNodeViewModel[] { hovered }
            : _nodes.Values.Where(node => node.IsCurrent);

        foreach (GenerationChatBranchNodeViewModel target in targets)
        {
            GenerationChatBranchNodeViewModel? current = target;

            while (current is not null && _highlightedNodes.Add(current.Id))
            {
                current = current.ParentId is Guid parent ? _nodes.GetValueOrDefault(parent) : null;
            }
        }

        foreach ((Guid child, Guid parent, GeometryDrawing drawing) in _connections)
        {
            drawing.Pen = _highlightedNodes.Contains(child) && _highlightedNodes.Contains(parent) ? _currentPen : _neutralPen;
        }

        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachNodes()
    {
        foreach (GenerationChatBranchNodeViewModel node in _nodes.Values)
        {
            node.PropertyChanged += OnNodeChanged;
        }
    }

    private void DetachNodes()
    {
        foreach (GenerationChatBranchNodeViewModel node in _nodes.Values)
        {
            node.PropertyChanged -= OnNodeChanged;
        }
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatBranchNodeViewModel.IsCurrent))
        {
            UpdateHighlightedRoute();
        }
        else if (e.PropertyName is nameof(GenerationChatBranchNodeViewModel.Id) or nameof(GenerationChatBranchNodeViewModel.ParentId))
        {
            _layoutInvalidated = true;
            InvalidateMeasure();
        }
    }
}
