using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls.Gallery;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationPreviewExpansionController
{
    private static readonly TimeSpan PreviewAnimationDuration = TimeSpan.FromSeconds(0.15d);

    private readonly GenerationPreviewControl _owner;
    private readonly Grid _previewExpansionHost;
    private readonly Border _previewShadow;
    private readonly Image _previewImage;
    private readonly Border _previewTrigger;
    private readonly TranslateTransform _previewTranslation = new();
    private readonly Transitions _sizeTransitions;
    private readonly GenerationPreviewOverlayPortal _overlayPortal;
    private IDisposable? _activeSizeTransitions;
    private CancellationTokenSource? _collapseCompletionCancellation;
    private IGenerationPreviewExpansionHost? _expansionHost;
    private Control? _overflowOwner;
    private KeyModifiers _currentKeyModifiers;
    private bool _isPointerInsidePreview;
    private bool _isPreviewExpanded;
    private Rect? _collapsedTriggerBounds;
    private Point? _overlayPointerPosition;

    internal GenerationPreviewExpansionController(
        GenerationPreviewControl owner,
        Grid previewExpansionHost,
        Border previewShadow,
        Image previewImage,
        Border previewTrigger)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _previewExpansionHost = previewExpansionHost
            ?? throw new ArgumentNullException(nameof(previewExpansionHost));
        _previewShadow = previewShadow ?? throw new ArgumentNullException(nameof(previewShadow));
        _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
        _previewTrigger = previewTrigger ?? throw new ArgumentNullException(nameof(previewTrigger));
        _overlayPortal = new GenerationPreviewOverlayPortal(previewExpansionHost);

        CubicEaseOut easing = new();
        _sizeTransitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Control.WidthProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = Control.HeightProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            }
        };
        InitializePositionAndShadowAnimation(easing);
        _owner.AddHandler(
            InputElement.PointerMovedEvent,
            OnPreviewPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _owner.PointerEntered += OnPreviewPointerMoved;
        _owner.PointerExited += OnPreviewPointerExited;
        _owner.AttachedToVisualTree += OnAttachedToVisualTree;
        _owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    internal static bool HasExpansionModifier(KeyModifiers modifiers)
    {
        return AlternateActionModifierPolicy.IsActive(modifiers);
    }

    internal static KeyModifiers GetExpansionModifier(Key key)
    {
        return key switch
        {
            Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
            Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
            Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
            _ => KeyModifiers.None
        };
    }

    internal void RefreshHost()
    {
        DetachHost();

        if (TopLevel.GetTopLevel(_owner) is null)
        {
            return;
        }

        _expansionHost = _owner.ExpansionHost;
        _overflowOwner = _owner.OverflowOwner ?? _owner;

        if (_expansionHost is not null)
        {
            _expansionHost.PointerStateChanged += OnHostPointerStateChanged;
            _expansionHost.ModifiersChanged += OnHostModifiersChanged;
            _expansionHost.Viewport.SizeChanged += OnViewportSizeChanged;
        }
    }

    private static bool HasPositiveSize(Size size)
    {
        return size is { Width: > 0d, Height: > 0d };
    }

    private void InitializePositionAndShadowAnimation(CubicEaseOut easing)
    {
        _previewExpansionHost.RenderTransform = _previewTranslation;
        _previewTranslation.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            }
        };
        _previewShadow.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            }
        };
    }

    private void UpdatePreviewExpansionState()
    {
        bool shouldExpand = _expansionHost is { IsExpansionEnabled: true }
            && _expansionHost.Viewport.IsPointerOver
            && _isPointerInsidePreview
            && (_expansionHost.ActiveOwner is null || ReferenceEquals(_expansionHost.ActiveOwner, _overflowOwner))
            && HasExpansionModifier(_currentKeyModifiers)
            && _previewImage.IsVisible;

        if (shouldExpand && TryExpandPreview())
        {
            return;
        }

        CollapsePreview();
    }

    private bool TryExpandPreview()
    {
        IGenerationPreviewExpansionHost? expansionHost = _expansionHost;
        Control? overflowOwner = _overflowOwner;
        IImage? source = _previewImage.Source;
        Size previewSize = _previewTrigger.Bounds.Size;
        Size viewportSize = expansionHost?.Viewport.Bounds.Size ?? default;

        if (expansionHost is null || overflowOwner is null)
        {
            return false;
        }

        Matrix? transform = _previewTrigger.TransformToVisual(expansionHost.Viewport);

        if (source is null
            || transform is not { HasInverse: true } matrix
            || !HasPositiveSize(previewSize)
            || !HasPositiveSize(source.Size)
            || !HasPositiveSize(viewportSize))
        {
            return false;
        }

        if (_isPreviewExpanded && _overlayPortal.IsPresented)
        {
            return true;
        }

        Rect triggerBounds = GetTriggerBounds();
        Rect previewBounds = triggerBounds.TransformToAABB(matrix);
        Size displayedSize = new Rect(previewSize).TransformToAABB(matrix).Size;
        Rect viewportBounds = new(viewportSize);
        Size expandedSize;
        Point localOrigin;

        if (expansionHost.ExpansionLayer is { } layer)
        {
            (Size size, Vector translation) = GenerationPreviewExpansionCalculator.CalculateFitted(
                previewSize, source.Size, previewBounds, viewportBounds);
            expandedSize = size;
            localOrigin = previewBounds.TopLeft + translation;
            PresentOverlay(layer, new Rect(previewSize).TransformToAABB(matrix));
        }
        else
        {
            (Size size, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
                displayedSize, source.Size, previewBounds, viewportBounds);
            expandedSize = new Size(size.Width * previewSize.Width / displayedSize.Width,
                size.Height * previewSize.Height / displayedSize.Height);
            localOrigin = (previewBounds.TopLeft + translation).Transform(matrix.Invert());
        }

        _collapsedTriggerBounds = triggerBounds;
        BeginPreviewExpansion(expansionHost, overflowOwner);
        SetExpansionSize(expandedSize);
        _previewTranslation.X = localOrigin.X;
        _previewTranslation.Y = localOrigin.Y;
        _previewShadow.Opacity = 1d;

        return true;
    }

    private void PresentOverlay(Panel layer, Rect initialBounds)
    {
        if (_overlayPortal.IsPresented)
        {
            return;
        }

        Transitions? transitions = _previewTranslation.Transitions;
        _previewTranslation.Transitions = null;
        SetExpansionSize(initialBounds.Size);
        _previewTranslation.X = initialBounds.X;
        _previewTranslation.Y = initialBounds.Y;
        _overlayPortal.Present(layer, _owner.DataContext);
        _previewTranslation.Transitions = transitions;
    }

    private Rect GetTriggerBounds()
    {
        if (_collapsedTriggerBounds is { } saved)
        {
            return saved;
        }

        Rect bounds = new(_previewTrigger.Bounds.Size);
        Visual? current = _previewTrigger.GetVisualParent();

        while (current is not null)
        {
            if (current.ClipToBounds && current.TransformToVisual(_previewTrigger) is { } transform)
            {
                bounds = bounds.Intersect(new Rect(current.Bounds.Size).TransformToAABB(transform));
            }

            if (object.ReferenceEquals(current, _overflowOwner))
            {
                break;
            }

            current = current.GetVisualParent();
        }

        return bounds;
    }

    private void BeginPreviewExpansion(
        IGenerationPreviewExpansionHost expansionHost,
        Control overflowOwner)
    {
        CancelPendingCollapseCompletion();

        if (_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = true;
        _activeSizeTransitions ??= _previewExpansionHost.SetValue(
            Visual.TransitionsProperty,
            _sizeTransitions,
            BindingPriority.Style);
        expansionHost.EnableOverflow(overflowOwner, _previewExpansionHost);
        overflowOwner.Classes.Add(GenerationPreviewExpansionVisualMetrics.ExpandedClass);

        if (_overlayPortal.IsPresented)
        {
            _overlayPointerPosition = expansionHost.PointerPosition;
            expansionHost.Viewport.AddHandler(InputElement.PointerMovedEvent, OnOverlayPointerMoved,
                RoutingStrategies.Tunnel, true);
        }
    }

    private void CollapsePreview()
    {
        if (!_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = false;
        _overlayPointerPosition = null;

        if (_expansionHost is not null && _overflowOwner is not null)
        {
            _expansionHost.BeginOverflowCollapse(_overflowOwner);
            _expansionHost.Viewport.RemoveHandler(InputElement.PointerMovedEvent, OnOverlayPointerMoved);
        }

        if (_overlayPortal.IsPresented && _expansionHost is not null
            && _previewTrigger.TransformToVisual(_expansionHost.Viewport) is { } matrix)
        {
            Rect collapsed = new Rect(_previewTrigger.Bounds.Size).TransformToAABB(matrix);
            SetExpansionSize(collapsed.Size);
            _previewTranslation.X = collapsed.X;
            _previewTranslation.Y = collapsed.Y;
        }
        else
        {
            SetExpansionSize(new Size(_owner.PreviewSize, _owner.PreviewSize));
            _previewTranslation.X = 0d;
            _previewTranslation.Y = 0d;
        }

        _previewShadow.Opacity = 0d;
        ScheduleOverflowRestore();
    }

    private void ScheduleOverflowRestore()
    {
        CancelPendingCollapseCompletion();
        CancellationTokenSource cancellation = new();
        _collapseCompletionCancellation = cancellation;
        _ = RestoreOverflowAsync(cancellation);
    }

    private async Task RestoreOverflowAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(PreviewAnimationDuration, cancellation.Token);

            if (!_isPreviewExpanded)
            {
                RestoreCollapsedPreviewImmediately();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_collapseCompletionCancellation, cancellation))
            {
                _collapseCompletionCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelPendingCollapseCompletion()
    {
        _collapseCompletionCancellation?.Cancel();
        _collapseCompletionCancellation = null;
    }

    private void RestoreCollapsedPreviewImmediately()
    {
        _isPreviewExpanded = false;
        _overlayPointerPosition = null;
        _expansionHost?.Viewport.RemoveHandler(InputElement.PointerMovedEvent, OnOverlayPointerMoved);
        _activeSizeTransitions?.Dispose();
        _activeSizeTransitions = null;
        Transitions? transitions = _previewTranslation.Transitions;
        _previewTranslation.Transitions = null;
        _previewExpansionHost.ClearValue(Control.WidthProperty);
        _previewExpansionHost.ClearValue(Control.HeightProperty);
        _previewTranslation.X = 0d;
        _previewTranslation.Y = 0d;
        _previewShadow.Opacity = 0d;
        _overlayPortal.Restore();
        _previewTranslation.Transitions = transitions;
        _overflowOwner?.Classes.Remove(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
        RestoreOverflow();
        _collapsedTriggerBounds = null;
    }

    private bool IsPointerInsidePreview(Point viewportPosition)
    {
        if (_expansionHost is null)
        {
            return false;
        }

        if (_isPreviewExpanded && _overlayPortal.IsPresented
            && _previewExpansionHost.TransformToVisual(_expansionHost.Viewport) is { } expandedTransform)
        {
            Size currentSize = new(_previewExpansionHost.Width, _previewExpansionHost.Height);

            return new Rect(currentSize).TransformToAABB(expandedTransform).Contains(viewportPosition);
        }

        Point? position = _expansionHost.Viewport.TranslatePoint(viewportPosition, _previewTrigger);

        return position is not null && GetTriggerBounds().Contains(position.Value);
    }

    private void SetExpansionSize(Size size)
    {
        _previewExpansionHost.Width = size.Width;
        _previewExpansionHost.Height = size.Height;
    }

    private void RestoreOverflow()
    {
        if (_expansionHost is not null && _overflowOwner is not null)
        {
            _expansionHost.DisableOverflow(_overflowOwner);
        }
    }

    private void DetachHost()
    {
        if (_expansionHost is not null)
        {
            _expansionHost.PointerStateChanged -= OnHostPointerStateChanged;
            _expansionHost.ModifiersChanged -= OnHostModifiersChanged;
            _expansionHost.Viewport.SizeChanged -= OnViewportSizeChanged;
        }

        _isPointerInsidePreview = false;
        _currentKeyModifiers = KeyModifiers.None;
        CancelPendingCollapseCompletion();
        RestoreCollapsedPreviewImmediately();
        _overflowOwner = null;
        _expansionHost = null;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        RefreshHost();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachHost();
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        _isPointerInsidePreview = _overlayPortal.IsPresented && _expansionHost is not null
            ? IsPointerInsidePreview(e.GetPosition(_expansionHost.Viewport))
            : GetTriggerBounds().Contains(e.GetPosition(_previewTrigger));
        _currentKeyModifiers = e.KeyModifiers;
        UpdatePreviewExpansionState();
    }

    private void OnPreviewPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;

        _isPointerInsidePreview = _overlayPortal.IsPresented && _expansionHost is not null
            && IsPointerInsidePreview(e.GetPosition(_expansionHost.Viewport));
        UpdatePreviewExpansionState();
    }

    private void OnHostPointerStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        IGenerationPreviewExpansionHost? expansionHost = _expansionHost;
        Point? pointerPosition = expansionHost?.PointerPosition;
        _isPointerInsidePreview = pointerPosition is not null && IsPointerInsidePreview(pointerPosition.Value);
        _currentKeyModifiers = expansionHost?.CurrentKeyModifiers ?? KeyModifiers.None;
        UpdatePreviewExpansionState();
    }

    private void OnHostModifiersChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _currentKeyModifiers = _expansionHost?.CurrentKeyModifiers ?? KeyModifiers.None;

        if (_expansionHost?.ExpansionLayer is not null)
        {
            _isPointerInsidePreview = _expansionHost.PointerPosition is { } position && IsPointerInsidePreview(position);
        }

        UpdatePreviewExpansionState();
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_expansionHost is not null)
        {
            Point position = e.GetPosition(_expansionHost.Viewport);
            bool pointerMoved = _overlayPointerPosition != position;
            _overlayPointerPosition = position;
            GenerationPreviewControl? hovered = (e.Source as Visual)?.GetSelfAndVisualAncestors()
                .OfType<GenerationPreviewControl>().FirstOrDefault();
            bool switchPreview = pointerMoved && hovered is not null && !ReferenceEquals(hovered, _owner)
                && ReferenceEquals(hovered.ExpansionHost, _expansionHost);
            _isPointerInsidePreview = !switchPreview && IsPointerInsidePreview(position);
            _currentKeyModifiers = e.KeyModifiers;
            UpdatePreviewExpansionState();
        }
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPointerInsidePreview = false;
        CollapsePreview();
    }
}
