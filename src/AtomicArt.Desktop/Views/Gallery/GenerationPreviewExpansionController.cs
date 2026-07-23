using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls.Gallery;

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
    private CancellationTokenSource? _collapseCompletionCancellation;
    private IGenerationPreviewExpansionHost? _expansionHost;
    private Control? _overflowOwner;
    private Size _collapsedPreviewSize;
    private KeyModifiers _currentKeyModifiers;
    private bool _isPointerInsidePreview;
    private bool _isPreviewExpanded;

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

        InitializeAnimation();
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
        KeyModifiers expansionModifiers = KeyModifiers.Shift
            | KeyModifiers.Control
            | KeyModifiers.Alt;

        return (modifiers & expansionModifiers) != KeyModifiers.None;
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

    private static bool HasPositiveSize(Size size)
    {
        return size is { Width: > 0d, Height: > 0d };
    }

    private void InitializeAnimation()
    {
        CubicEaseOut easing = new();
        _previewExpansionHost.RenderTransform = _previewTranslation;
        _previewExpansionHost.Transitions =
        [
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
        ];
        _previewTranslation.Transitions =
        [
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
        ];
        _previewShadow.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            }
        ];
    }

    private void UpdatePreviewExpansionState()
    {
        bool shouldExpand = _isPointerInsidePreview
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

        Point? previewPosition = _previewTrigger.TranslatePoint(
            new Point(0d, 0d),
            expansionHost.Viewport);

        if (source is null
            || previewPosition is null
            || !HasPositiveSize(previewSize)
            || !HasPositiveSize(source.Size)
            || !HasPositiveSize(viewportSize))
        {
            return false;
        }

        Rect previewBounds = new(previewPosition.Value, previewSize);
        Rect viewportBounds = new(viewportSize);
        (Size expandedSize, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
            previewSize,
            source.Size,
            previewBounds,
            viewportBounds);

        _collapsedPreviewSize = previewSize;
        BeginPreviewExpansion(expansionHost, overflowOwner);
        _previewExpansionHost.Width = expandedSize.Width;
        _previewExpansionHost.Height = expandedSize.Height;
        _previewTranslation.X = translation.X;
        _previewTranslation.Y = translation.Y;
        _previewShadow.Opacity = 1d;

        return true;
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
        expansionHost.EnableOverflow(overflowOwner, _previewExpansionHost);
        overflowOwner.Classes.Add(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
    }

    private void CollapsePreview()
    {
        if (!_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = false;

        if (_expansionHost is not null && _overflowOwner is not null)
        {
            _expansionHost.BeginOverflowCollapse(_overflowOwner);
            _overflowOwner.Classes.Remove(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
        }

        _previewExpansionHost.Width = _collapsedPreviewSize.Width;
        _previewExpansionHost.Height = _collapsedPreviewSize.Height;
        _previewTranslation.X = 0d;
        _previewTranslation.Y = 0d;
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
                RestoreOverflow();
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
        _overflowOwner?.Classes.Remove(GenerationPreviewExpansionVisualMetrics.ExpandedClass);

        if (HasPositiveSize(_collapsedPreviewSize))
        {
            _previewExpansionHost.Width = _collapsedPreviewSize.Width;
            _previewExpansionHost.Height = _collapsedPreviewSize.Height;
        }

        _previewTranslation.X = 0d;
        _previewTranslation.Y = 0d;
        _previewShadow.Opacity = 0d;
        RestoreOverflow();
    }

    private void RestoreOverflow()
    {
        if (_expansionHost is not null && _overflowOwner is not null)
        {
            _expansionHost.DisableOverflow(_overflowOwner);
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        _expansionHost = _owner.ExpansionHost;
        _overflowOwner = _owner.OverflowOwner ?? _owner;

        if (_expansionHost is null)
        {
            return;
        }

        _expansionHost.PointerStateChanged += OnHostPointerStateChanged;
        _expansionHost.Viewport.SizeChanged += OnViewportSizeChanged;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_expansionHost is not null)
        {
            _expansionHost.PointerStateChanged -= OnHostPointerStateChanged;
            _expansionHost.Viewport.SizeChanged -= OnViewportSizeChanged;
        }

        _isPointerInsidePreview = false;
        _currentKeyModifiers = KeyModifiers.None;
        CancelPendingCollapseCompletion();
        RestoreCollapsedPreviewImmediately();
        _overflowOwner = null;
        _expansionHost = null;
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        Point previewPosition = e.GetPosition(_previewTrigger);
        _isPointerInsidePreview = new Rect(_previewTrigger.Bounds.Size).Contains(previewPosition);
        _currentKeyModifiers = e.KeyModifiers;
        UpdatePreviewExpansionState();
    }

    private void OnPreviewPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPointerInsidePreview = false;
        UpdatePreviewExpansionState();
    }

    private void OnHostPointerStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        IGenerationPreviewExpansionHost? expansionHost = _expansionHost;
        Point? pointerPosition = expansionHost?.PointerPosition;
        Point? triggerPosition = expansionHost is null
            ? null
            : _previewTrigger.TranslatePoint(
                new Point(0d, 0d),
                expansionHost.Viewport);
        _isPointerInsidePreview = pointerPosition is not null
            && triggerPosition is not null
            && new Rect(triggerPosition.Value, _previewTrigger.Bounds.Size)
                .Contains(pointerPosition.Value);
        _currentKeyModifiers = expansionHost?.CurrentKeyModifiers ?? KeyModifiers.None;
        UpdatePreviewExpansionState();
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPointerInsidePreview = false;
        CollapsePreview();
    }
}
