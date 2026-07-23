using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class StandaloneGenerationPreviewExpansionHost : IGenerationPreviewExpansionHost
{
    public Control Viewport => _owner;
    public KeyModifiers CurrentKeyModifiers => _currentKeyModifiers;
    public Point? PointerPosition => _pointerPosition;

    public event EventHandler? PointerStateChanged;

    private readonly Control _owner;
    private readonly Dictionary<Control, int> _originalZIndices = [];
    private TopLevel? _topLevel;
    private Point? _pointerPosition;
    private KeyModifiers _currentKeyModifiers;

    public StandaloneGenerationPreviewExpansionHost(Control owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _owner.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _owner.PointerExited += OnPointerExited;
    }

    public void Attach()
    {
        DetachKeyboardHandlers();
        _topLevel = TopLevel.GetTopLevel(_owner);
        _topLevel?.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _topLevel?.AddHandler(
            InputElement.KeyUpEvent,
            OnKeyUp,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
    }

    public void Detach()
    {
        DetachKeyboardHandlers();
        _pointerPosition = null;
        _currentKeyModifiers = KeyModifiers.None;

        foreach ((Control control, int originalZIndex) in _originalZIndices)
        {
            control.ZIndex = originalZIndex;
            control.Classes.Remove(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
        }

        _originalZIndices.Clear();
    }

    public void EnableOverflow(Control owner, Visual preview)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(preview);

        if (!_originalZIndices.ContainsKey(owner))
        {
            _originalZIndices.Add(owner, owner.ZIndex);
        }

        owner.ZIndex = GenerationPreviewExpansionVisualMetrics.ActiveZIndex;
    }

    public void BeginOverflowCollapse(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_originalZIndices.ContainsKey(owner))
        {
            owner.ZIndex = GenerationPreviewExpansionVisualMetrics.CollapsingZIndex;
        }
    }

    public void DisableOverflow(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_originalZIndices.Remove(owner, out int originalZIndex))
        {
            owner.ZIndex = originalZIndex;
        }
    }

    private void DetachKeyboardHandlers()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _topLevel.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        _topLevel = null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        _pointerPosition = e.GetPosition(_owner);
        _currentKeyModifiers = e.KeyModifiers;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        _pointerPosition = null;
        _currentKeyModifiers = KeyModifiers.None;
        PointerStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdateModifiers(e, PreviewKeyTransition.Down);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdateModifiers(e, PreviewKeyTransition.Up);
    }

    private void UpdateModifiers(KeyEventArgs e, PreviewKeyTransition transition)
    {
        KeyModifiers modifier = GenerationPreviewExpansionController.GetExpansionModifier(e.Key);
        if (modifier == KeyModifiers.None)
        {
            return;
        }

        _currentKeyModifiers = transition switch
        {
            PreviewKeyTransition.Down => e.KeyModifiers | modifier,
            PreviewKeyTransition.Up => e.KeyModifiers & ~modifier,
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null)
        };
        PointerStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private enum PreviewKeyTransition
    {
        Down,
        Up
    }
}
