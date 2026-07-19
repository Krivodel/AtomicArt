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
    private const string PreviewExpandedClass = "preview-expanded";
    private const int ActivePreviewZIndex = 1001;
    private const int CollapsingPreviewZIndex = 1000;

    private static readonly TimeSpan PreviewAnimationDuration = TimeSpan.FromSeconds(0.15d);

    private readonly GenerationPreviewControl _owner;
    private readonly Grid _previewExpansionHost;
    private readonly Border _previewShadow;
    private readonly Image _previewImage;
    private readonly Border _previewTrigger;
    private readonly TranslateTransform _previewTranslation = new();

    private CancellationTokenSource? _collapseCompletionCancellation;
    private AnimatedGalleryControl? _galleryControl;
    private ScrollViewer? _galleryScrollViewer;
    private Control? _overflowOwner;
    private Control? _standaloneViewport;
    private TopLevel? _topLevel;
    private Size _collapsedPreviewSize;
    private KeyModifiers _currentKeyModifiers;
    private int _originalStandaloneZIndex;
    private bool _isPointerInsidePreview;
    private bool _isPreviewExpanded;
    private bool _hasStandaloneOverflow;

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
        Transitions hostTransitions =
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
        Transitions translationTransitions =
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
        Transitions shadowTransitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = PreviewAnimationDuration,
                Easing = easing
            }
        ];

        _previewExpansionHost.RenderTransform = _previewTranslation;
        _previewExpansionHost.Transitions = hostTransitions;
        _previewTranslation.Transitions = translationTransitions;
        _previewShadow.Transitions = shadowTransitions;
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
        Control? viewport = _galleryScrollViewer ?? _standaloneViewport;
        IImage? source = _previewImage.Source;
        Size previewSize = _previewTrigger.Bounds.Size;
        Size viewportSize = viewport?.Bounds.Size ?? default;

        if (viewport is null || _overflowOwner is null)
        {
            return false;
        }

        Point? previewPosition = _previewTrigger.TranslatePoint(
            new Point(0d, 0d),
            viewport);

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
        BeginPreviewExpansion();
        _previewExpansionHost.Width = expandedSize.Width;
        _previewExpansionHost.Height = expandedSize.Height;
        _previewTranslation.X = translation.X;
        _previewTranslation.Y = translation.Y;
        _previewShadow.Opacity = 1d;

        return true;
    }

    private void BeginPreviewExpansion()
    {
        CancelPendingCollapseCompletion();

        if (_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = true;
        Control overflowOwner = _overflowOwner
            ?? throw new InvalidOperationException("Preview overflow owner is unavailable.");
        _galleryControl?.EnablePreviewOverflow(overflowOwner, _previewExpansionHost);

        if (_galleryControl is null)
        {
            _originalStandaloneZIndex = overflowOwner.ZIndex;
            overflowOwner.ZIndex = ActivePreviewZIndex;
            _hasStandaloneOverflow = true;
        }

        overflowOwner.Classes.Add(PreviewExpandedClass);
    }

    private void CollapsePreview()
    {
        if (!_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = false;
        Control? overflowOwner = _overflowOwner;

        if (overflowOwner is not null)
        {
            _galleryControl?.BeginPreviewOverflowCollapse(overflowOwner);

            if (_galleryControl is null && _hasStandaloneOverflow)
            {
                overflowOwner.ZIndex = CollapsingPreviewZIndex;
            }

            overflowOwner.Classes.Remove(PreviewExpandedClass);
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
        _overflowOwner?.Classes.Remove(PreviewExpandedClass);

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
        Control? overflowOwner = _overflowOwner;
        if (overflowOwner is null)
        {
            return;
        }

        _galleryControl?.DisablePreviewOverflow(overflowOwner);

        if (_hasStandaloneOverflow)
        {
            overflowOwner.ZIndex = _originalStandaloneZIndex;
            _hasStandaloneOverflow = false;
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        _galleryControl = _owner
            .GetVisualAncestors()
            .OfType<AnimatedGalleryControl>()
            .FirstOrDefault();
        _galleryScrollViewer = _galleryControl?.PreviewScrollViewer;
        _overflowOwner = (Control?)_owner
            .GetVisualAncestors()
            .OfType<GenerationCardControl>()
            .FirstOrDefault()
            ?? _owner;
        _standaloneViewport = _galleryScrollViewer is null
            ? _owner
                .GetVisualAncestors()
                .OfType<GenerationMetadataOverlayView>()
                .FirstOrDefault()
            : null;

        if (_galleryControl is not null)
        {
            _galleryControl.PreviewPointerStateChanged += OnGalleryPointerStateChanged;
        }

        if (_galleryScrollViewer is not null)
        {
            _galleryScrollViewer.SizeChanged += OnViewportSizeChanged;
        }

        if (_galleryControl is null)
        {
            AttachStandaloneKeyboardHandlers();

            if (_standaloneViewport is not null)
            {
                _standaloneViewport.SizeChanged += OnViewportSizeChanged;
            }
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_galleryScrollViewer is not null)
        {
            _galleryScrollViewer.SizeChanged -= OnViewportSizeChanged;
            _galleryScrollViewer = null;
        }

        if (_standaloneViewport is not null)
        {
            _standaloneViewport.SizeChanged -= OnViewportSizeChanged;
            _standaloneViewport = null;
        }

        if (_galleryControl is not null)
        {
            _galleryControl.PreviewPointerStateChanged -= OnGalleryPointerStateChanged;
        }

        DetachStandaloneKeyboardHandlers();
        _isPointerInsidePreview = false;
        _currentKeyModifiers = KeyModifiers.None;
        CancelPendingCollapseCompletion();
        RestoreCollapsedPreviewImmediately();
        _overflowOwner = null;
        _galleryControl = null;
    }

    private void AttachStandaloneKeyboardHandlers()
    {
        DetachStandaloneKeyboardHandlers();
        _topLevel = TopLevel.GetTopLevel(_owner);
        _topLevel?.AddHandler(
            InputElement.KeyDownEvent,
            OnStandaloneKeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _topLevel?.AddHandler(
            InputElement.KeyUpEvent,
            OnStandaloneKeyUp,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
    }

    private void DetachStandaloneKeyboardHandlers()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnStandaloneKeyDown);
        _topLevel.RemoveHandler(InputElement.KeyUpEvent, OnStandaloneKeyUp);
        _topLevel = null;
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

    private void OnGalleryPointerStateChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        Point? pointerPosition = _galleryControl?.GetPreviewPointerPosition();
        Point? triggerPosition = _galleryScrollViewer is null
            ? null
            : _previewTrigger.TranslatePoint(
                new Point(0d, 0d),
                _galleryScrollViewer);
        _isPointerInsidePreview = pointerPosition is not null
            && triggerPosition is not null
            && new Rect(triggerPosition.Value, _previewTrigger.Bounds.Size)
                .Contains(pointerPosition.Value);
        _currentKeyModifiers = _galleryControl?.GetPreviewPointerModifiers()
            ?? KeyModifiers.None;
        UpdatePreviewExpansionState();
    }

    private void OnStandaloneKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdateStandaloneModifiers(e, PreviewKeyTransition.Down);
    }

    private void OnStandaloneKeyUp(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdateStandaloneModifiers(e, PreviewKeyTransition.Up);
    }

    private void UpdateStandaloneModifiers(
        KeyEventArgs e,
        PreviewKeyTransition transition)
    {
        KeyModifiers modifier = GetExpansionModifier(e.Key);
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
        UpdatePreviewExpansionState();
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPointerInsidePreview = false;
        CollapsePreview();
    }

    private enum PreviewKeyTransition
    {
        Down,
        Up
    }
}
