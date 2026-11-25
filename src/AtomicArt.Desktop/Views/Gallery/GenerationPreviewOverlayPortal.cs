using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationPreviewOverlayPortal
{
    public bool IsPresented => _inlineParent is not null;

    private readonly Control _content;
    private Panel? _inlineParent;
    private Panel? _layer;
    private int _inlineIndex;
    private IDisposable? _dataContextValue;

    public GenerationPreviewOverlayPortal(Control content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public void Present(Panel layer, object? dataContext)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (IsPresented)
        {
            return;
        }

        _inlineParent = _content.GetVisualParent() as Panel
            ?? throw new InvalidOperationException("The generation preview has no inline panel.");
        _inlineIndex = _inlineParent.Children.IndexOf(_content);
        _dataContextValue = _content.SetValue(StyledElement.DataContextProperty, dataContext, BindingPriority.Style);
        _inlineParent.Children.Remove(_content);
        _layer = layer;
        layer.Children.Add(_content);
    }

    public void Restore()
    {
        if (_inlineParent is null)
        {
            return;
        }

        _layer?.Children.Remove(_content);
        _inlineParent.Children.Insert(Math.Min(_inlineIndex, _inlineParent.Children.Count), _content);
        _dataContextValue?.Dispose();
        _dataContextValue = null;
        _inlineParent = null;
        _layer = null;
    }
}
