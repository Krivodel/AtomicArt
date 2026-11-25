using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class GenerationChatBranchConnectionsControl : Decorator
{
    private readonly Canvas _connections = new() { IsHitTestVisible = false };
    private DrawingGroup? _drawing;
    private GenerationChatBranchPanel? _panel;

    public GenerationChatBranchConnectionsControl()
    {
        LogicalChildren.Add(_connections);
        VisualChildren.Add(_connections);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size size = base.MeasureOverride(availableSize);
        AttachPanel();
        _connections.Measure(size);

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _connections.Arrange(new Rect(finalSize));

        return base.ArrangeOverride(finalSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachPanel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.ConnectionsChanged -= OnConnectionsChanged;
            _panel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void AttachPanel()
    {
        GenerationChatBranchPanel? panel = (Child as ItemsControl)?.ItemsPanelRoot as GenerationChatBranchPanel;

        if (ReferenceEquals(_panel, panel))
        {
            return;
        }

        if (_panel is not null)
        {
            _panel.ConnectionsChanged -= OnConnectionsChanged;
        }

        _panel = panel;

        if (_panel is not null)
        {
            _panel.ConnectionsChanged += OnConnectionsChanged;
        }

        RefreshConnections();
    }

    private void RefreshConnections()
    {
        DrawingGroup? drawing = _panel?.ConnectionDrawing;

        if (!ReferenceEquals(_drawing, drawing))
        {
            _drawing = drawing;
            _connections.Children.Clear();

            if (drawing is not null)
            {
                foreach (GeometryDrawing connection in drawing.Children.OfType<GeometryDrawing>())
                {
                    _connections.Children.Add(new GenerationChatBranchConnectionControl(connection));
                }
            }
        }
        else
        {
            foreach (GenerationChatBranchConnectionControl connection in _connections.Children.OfType<GenerationChatBranchConnectionControl>())
            {
                connection.RefreshPen();
            }
        }
    }

    private void OnConnectionsChanged(object? sender, EventArgs e)
    {
        RefreshConnections();
    }
}
