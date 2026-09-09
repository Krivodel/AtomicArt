using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGallerySelectionBrushController
{
    private const int SelectionLongPressMilliseconds = 400;

    private readonly AnimatedGalleryControl _owner;
    private readonly DispatcherTimer _selectionLongPressTimer;
    private SelectionBrushCandidate? _candidate;
    private SelectionBrushSession? _session;
    private bool _isSessionSelectionEnabled;

    public AnimatedGallerySelectionBrushController(
        AnimatedGalleryControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _selectionLongPressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SelectionLongPressMilliseconds)
        };
        _selectionLongPressTimer.Tick += OnSelectionLongPressTimerTick;
        _owner.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _owner.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _owner.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _owner.PointerCaptureLost += OnPointerCaptureLost;
    }

    public void Cancel()
    {
        StopSelectionLongPressTimer();
        _candidate = null;
        IPointer? pointer = _session?.Pointer;
        _session = null;
        _isSessionSelectionEnabled = false;
        pointer?.Capture(null);
    }

    public void HandleSelectionModeEnded()
    {
        StopSelectionLongPressTimer();
        _candidate = null;
        _isSessionSelectionEnabled = false;
    }

    private SelectionBrushHit? GetHitAt(Point position)
    {
        IInputElement? hit = _owner.InputHitTest(position);
        Visual? visual = hit as Visual;
        Visual? hitVisual = visual;

        while (visual is not null)
        {
            if (visual is GenerationCardControl card
                && card.DataContext is GenerationItemViewModel item
                && hitVisual is not null)
            {
                if (card.IsSelectionGestureBlockedHit(hitVisual))
                {
                    return null;
                }

                return new SelectionBrushHit(
                    item);
            }

            if (ReferenceEquals(visual, _owner))
            {
                return null;
            }

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private void ApplySelection(
        GenerationItemViewModel? item,
        bool isSelected)
    {
        if (item is null
            || item.IsSelected == isSelected)
        {
            return;
        }

        IRelayCommand? command = _owner.ToggleSelectionCommand;

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }

    private void StopSelectionLongPressTimer()
    {
        _selectionLongPressTimer.Stop();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        PointerPoint pointerPoint = e.GetCurrentPoint(_owner);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        SelectionBrushHit? hit = GetHitAt(pointerPoint.Position);
        if (hit is null)
        {
            return;
        }

        StopSelectionLongPressTimer();
        _candidate = new SelectionBrushCandidate(
            e.Pointer,
            pointerPoint.Position,
            hit.Item,
            _owner.IsSelectionMode ? !hit.Item.IsSelected : true);

        if (!_owner.IsSelectionMode)
        {
            _selectionLongPressTimer.Start();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        PointerPoint pointerPoint = e.GetCurrentPoint(_owner);

        if (_session is SelectionBrushSession session)
        {
            if (!ReferenceEquals(session.Pointer, e.Pointer)
                || !pointerPoint.Properties.IsLeftButtonPressed)
            {
                Cancel();
                return;
            }

            if (_isSessionSelectionEnabled)
            {
                ApplySelection(
                    GetHitAt(pointerPoint.Position)?.Item,
                    session.TargetIsSelected);
            }

            e.Handled = true;
            return;
        }

        SelectionBrushCandidate? candidate = _candidate;
        if (candidate is null
            || !ReferenceEquals(candidate.Pointer, e.Pointer)
            || !pointerPoint.Properties.IsLeftButtonPressed)
        {
            _candidate = null;
            return;
        }

        if (!_owner.IsSelectionMode
            && PointerDragThreshold.IsReached(
                candidate.Origin,
                pointerPoint.Position))
        {
            Cancel();
            return;
        }

        if (!PointerDragThreshold.IsReached(
                candidate.Origin,
                pointerPoint.Position))
        {
            return;
        }

        SelectionBrushHit? currentHit = GetHitAt(
            pointerPoint.Position);
        if (currentHit is null)
        {
            return;
        }

        _candidate = null;
        _session = new SelectionBrushSession(
            e.Pointer,
            candidate.TargetIsSelected);
        _isSessionSelectionEnabled = true;
        e.Pointer.Capture(_owner);
        ApplySelection(candidate.StartItem, candidate.TargetIsSelected);

        if ((_session is not null) && _isSessionSelectionEnabled)
        {
            ApplySelection(
                currentHit.Item,
                candidate.TargetIsSelected);
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;

        if (_session is null)
        {
            StopSelectionLongPressTimer();
            _candidate = null;
            return;
        }

        Cancel();
        e.Handled = true;
    }

    private void OnPointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs e)
    {
        _ = sender;
        _ = e;

        StopSelectionLongPressTimer();
        _candidate = null;
        _session = null;
        _isSessionSelectionEnabled = false;
    }

    private void OnSelectionLongPressTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _selectionLongPressTimer.Stop();

        SelectionBrushCandidate? candidate = _candidate;
        if (candidate is null || _owner.IsSelectionMode)
        {
            return;
        }

        _candidate = null;
        _session = new SelectionBrushSession(
            candidate.Pointer,
            candidate.TargetIsSelected);
        _isSessionSelectionEnabled = true;
        candidate.Pointer.Capture(_owner);
        ApplySelection(candidate.StartItem, candidate.TargetIsSelected);
    }

    private sealed record SelectionBrushCandidate(
        IPointer Pointer,
        Point Origin,
        GenerationItemViewModel StartItem,
        bool TargetIsSelected);

    private sealed record SelectionBrushSession(
        IPointer Pointer,
        bool TargetIsSelected);

    private sealed record SelectionBrushHit(GenerationItemViewModel Item);
}
