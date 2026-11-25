using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class StandaloneGenerationPreviewExpansionHost : IGenerationPreviewExpansionHost
{
    public bool IsExpansionEnabled => true;
    public Control Viewport => _owner;
    public Panel? ExpansionLayer { get; }
    public Control? ActiveOwner { get; private set; }
    public KeyModifiers CurrentKeyModifiers => _currentKeyModifiers;
    public Point? PointerPosition => _pointerPosition;

    public event EventHandler? PointerStateChanged;
    public event EventHandler? ModifiersChanged;

    private readonly Control _owner;
    private readonly GenerationPreviewOverflowController _overflow;
    private TopLevel? _topLevel;
    private Point? _pointerPosition;
    private KeyModifiers _currentKeyModifiers;

    public StandaloneGenerationPreviewExpansionHost(Control owner, Panel? expansionLayer = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ExpansionLayer = expansionLayer;
        _overflow = new GenerationPreviewOverflowController(owner);
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

        _overflow.Reset();
        ActiveOwner = null;
    }

    public void EnableOverflow(Control owner, Visual preview)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(preview);

        if (ExpansionLayer is null)
        {
            _overflow.Enable(owner, preview);
        }
        else
        {
            ActiveOwner = owner;
        }
    }

    public void BeginOverflowCollapse(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (ReferenceEquals(ActiveOwner, owner))
        {
            ActiveOwner = null;
        }

        _overflow.BeginCollapse(owner);
    }

    public void DisableOverflow(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (ReferenceEquals(ActiveOwner, owner))
        {
            ActiveOwner = null;
        }

        _overflow.Disable(owner);
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
        ModifiersChanged?.Invoke(this, EventArgs.Empty);
    }

    private enum PreviewKeyTransition
    {
        Down,
        Up
    }
}
