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

    private static readonly TimeSpan PreviewAnimationDuration = TimeSpan.FromSeconds(0.15d);

    private readonly GenerationCardControl _owner;
    private readonly Border _cardRoot;
    private readonly Grid _previewExpansionHost;
    private readonly Border _previewShadow;
    private readonly Image _previewImage;
    private readonly Border _previewTrigger;
    private readonly Button _revealInFolderButton;
    private readonly Button _deleteOrCancelButton;
    private readonly TranslateTransform _previewTranslation = new();

    private CancellationTokenSource? _collapseCompletionCancellation;
    private AnimatedGalleryControl? _galleryControl;
    private ScrollViewer? _galleryScrollViewer;
    private Size _collapsedPreviewSize;
    private KeyModifiers _currentKeyModifiers;
    private bool _isPointerInsidePreview;
    private bool _isPreviewExpanded;

    internal GenerationPreviewExpansionController(
        GenerationCardControl owner,
        Border cardRoot,
        Grid previewExpansionHost,
        Border previewShadow,
        Image previewImage,
        Border previewTrigger,
        Button revealInFolderButton,
        Button deleteOrCancelButton)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _cardRoot = cardRoot ?? throw new ArgumentNullException(nameof(cardRoot));
        _previewExpansionHost = previewExpansionHost
            ?? throw new ArgumentNullException(nameof(previewExpansionHost));
        _previewShadow = previewShadow ?? throw new ArgumentNullException(nameof(previewShadow));
        _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
        _previewTrigger = previewTrigger ?? throw new ArgumentNullException(nameof(previewTrigger));
        _revealInFolderButton = revealInFolderButton
            ?? throw new ArgumentNullException(nameof(revealInFolderButton));
        _deleteOrCancelButton = deleteOrCancelButton
            ?? throw new ArgumentNullException(nameof(deleteOrCancelButton));

        InitializeAnimation();
        _owner.AddHandler(
            InputElement.PointerMovedEvent,
            OnCardPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _owner.PointerEntered += OnCardPointerMoved;
        _owner.PointerExited += OnCardPointerExited;
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
        ScrollViewer? scrollViewer = _galleryScrollViewer;
        IImage? source = _previewImage.Source;
        Size previewSize = _previewTrigger.Bounds.Size;
        Size viewportSize = scrollViewer?.Viewport ?? default;

        if (scrollViewer is null || _galleryControl is null)
        {
            return false;
        }

        Point? previewPosition = _previewTrigger.TranslatePoint(
            new Point(0d, 0d),
            scrollViewer);

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
        _galleryControl?.EnablePreviewOverflow(_owner, _previewExpansionHost);
        _cardRoot.Classes.Add(PreviewExpandedClass);
        _revealInFolderButton.IsHitTestVisible = false;
        _deleteOrCancelButton.IsHitTestVisible = false;
    }

    private void CollapsePreview()
    {
        if (!_isPreviewExpanded)
        {
            return;
        }

        _isPreviewExpanded = false;
        _galleryControl?.BeginPreviewOverflowCollapse(_owner);
        _cardRoot.Classes.Remove(PreviewExpandedClass);
        _revealInFolderButton.IsHitTestVisible = true;
        _deleteOrCancelButton.IsHitTestVisible = true;
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
                _galleryControl?.DisablePreviewOverflow(_owner);
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
        _cardRoot.Classes.Remove(PreviewExpandedClass);
        _revealInFolderButton.IsHitTestVisible = true;
        _deleteOrCancelButton.IsHitTestVisible = true;

        if (HasPositiveSize(_collapsedPreviewSize))
        {
            _previewExpansionHost.Width = _collapsedPreviewSize.Width;
            _previewExpansionHost.Height = _collapsedPreviewSize.Height;
        }

        _previewTranslation.X = 0d;
        _previewTranslation.Y = 0d;
        _previewShadow.Opacity = 0d;
        _galleryControl?.DisablePreviewOverflow(_owner);
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

        if (_galleryControl is not null)
        {
            _galleryControl.PreviewPointerStateChanged += OnGalleryPointerStateChanged;
        }

        if (_galleryScrollViewer is not null)
        {
            _galleryScrollViewer.SizeChanged += OnGalleryScrollViewerSizeChanged;
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_galleryScrollViewer is not null)
        {
            _galleryScrollViewer.SizeChanged -= OnGalleryScrollViewerSizeChanged;
            _galleryScrollViewer = null;
        }

        if (_galleryControl is not null)
        {
            _galleryControl.PreviewPointerStateChanged -= OnGalleryPointerStateChanged;
        }

        _isPointerInsidePreview = false;
        _currentKeyModifiers = KeyModifiers.None;
        CancelPendingCollapseCompletion();
        RestoreCollapsedPreviewImmediately();
        _galleryControl = null;
    }

    private void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        Point previewPosition = e.GetPosition(_previewTrigger);
        _isPointerInsidePreview = new Rect(_previewTrigger.Bounds.Size).Contains(previewPosition);
        _currentKeyModifiers = e.KeyModifiers;
        UpdatePreviewExpansionState();
    }

    private void OnCardPointerExited(object? sender, PointerEventArgs e)
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

    private void OnGalleryScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPointerInsidePreview = false;
        CollapsePreview();
    }
}
