using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

public partial class AnimatedGalleryControl : UserControl
{
    internal const double BottomOpacityFadeHeight = 30d;

    public IEnumerable<IGalleryItemViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
    public object? RevealInFolderCommand
    {
        get => GetValue(RevealInFolderCommandProperty);
        set => SetValue(RevealInFolderCommandProperty, value);
    }
    public object? OpenViewerCommand
    {
        get => GetValue(OpenViewerCommandProperty);
        set => SetValue(OpenViewerCommandProperty, value);
    }
    public object? OpenMetadataCommand
    {
        get => GetValue(OpenMetadataCommandProperty);
        set => SetValue(OpenMetadataCommandProperty, value);
    }
    public object? DeleteOrCancelCommand
    {
        get => GetValue(DeleteOrCancelCommandProperty);
        set => SetValue(DeleteOrCancelCommandProperty, value);
    }
    public IAnimatedGalleryOperations? Operations
    {
        get => GetValue(OperationsProperty);
        set => SetValue(OperationsProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<IGalleryItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IEnumerable<IGalleryItemViewModel>?>(
            nameof(Items));
    public static readonly StyledProperty<object?> RevealInFolderCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, object?>(
            nameof(RevealInFolderCommand));
    public static readonly StyledProperty<object?> OpenViewerCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, object?>(
            nameof(OpenViewerCommand));
    public static readonly StyledProperty<object?> OpenMetadataCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, object?>(
            nameof(OpenMetadataCommand));
    public static readonly StyledProperty<object?> DeleteOrCancelCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, object?>(
            nameof(DeleteOrCancelCommand));
    public static readonly StyledProperty<IAnimatedGalleryOperations?> OperationsProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IAnimatedGalleryOperations?>(
            nameof(Operations));

    internal ScrollViewer PreviewScrollViewer => GalleryScrollViewer;
    internal IGenerationPreviewExpansionHost PreviewExpansionHost { get; }

    internal event EventHandler? PreviewPointerStateChanged;

    private AnimatedGalleryResizeController ResizeController =>
        _resizeController ?? throw new InvalidOperationException("Animated gallery resize controller was not created.");
    
    private readonly AnimatedGallerySceneController _sceneController;
    private readonly CollectionChangedSubscription _itemsSubscription;
    private readonly Dictionary<Control, int> _previewOriginalZIndices = [];
    private readonly Dictionary<Control, IReadOnlyList<Visual>> _previewOverflowPaths = [];
    private readonly Dictionary<Visual, PreviewClipState> _previewClipStates = [];
    private readonly HashSet<Control> _previewOverflowCards = [];

    private AnimatedGalleryResizeController? _resizeController;
    private TopLevel? _previewTopLevel;
    private Point? _previewPointerPosition;
    private KeyModifiers _previewPointerModifiers;
    private bool _isPreviewPointerRefreshPending;
    private bool _isAttached;

    public AnimatedGalleryControl()
        : this(null)
    {
    }

    internal AnimatedGalleryControl(IAnimatedGallerySceneFactory? sceneFactory)
    {
        _itemsSubscription = new CollectionChangedSubscription(OnItemsCollectionChanged);
        InitializeComponent();
        PreviewExpansionHost = new AnimatedGalleryPreviewExpansionHost(this);
        _sceneController = new AnimatedGallerySceneController(
            this,
            GalleryScrollViewer,
            GalleryPanel,
            OverlayCanvas,
            sceneFactory,
            () => _isAttached,
            CancelResizeAnimation);
        AddHandler(
            PointerMovedEvent,
            OnPreviewPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        AddHandler(
            PointerWheelChangedEvent,
            OnPreviewPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        PointerEntered += OnPreviewPointerMoved;
        PointerExited += OnPreviewPointerExited;
        GalleryScrollViewer.ScrollChanged += OnPreviewScrollChanged;
        _sceneController.RefreshItems();
    }

    internal Guid GetItemId(object item)
    {
        if (item is IGalleryItemViewModel galleryItem)
        {
            return galleryItem.Id;
        }

        throw new InvalidOperationException($"Gallery item '{item.GetType().Name}' does not expose a supported identifier.");
    }

    internal static double CalculateBottomFadeStartOffset(double height)
    {
        if (height <= BottomOpacityFadeHeight)
        {
            return 0d;
        }

        return (height - BottomOpacityFadeHeight) / height;
    }

    internal Point? GetPreviewPointerPosition()
    {
        return _previewPointerPosition;
    }

    internal KeyModifiers GetPreviewPointerModifiers()
    {
        return _previewPointerModifiers;
    }

    internal void EnablePreviewOverflow(Control card, Visual preview)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(preview);

        if (!_previewOverflowCards.Add(card))
        {
            card.ZIndex = GenerationPreviewExpansionVisualMetrics.ActiveZIndex;
            return;
        }

        _previewOriginalZIndices.Add(card, card.ZIndex);
        card.ZIndex = GenerationPreviewExpansionVisualMetrics.ActiveZIndex;
        IReadOnlyList<Visual> overflowPath = GetPreviewOverflowPath(preview);
        _previewOverflowPaths.Add(card, overflowPath);

        foreach (Visual visual in overflowPath)
        {
            DisableVisualClipping(visual);
        }

    }

    internal void BeginPreviewOverflowCollapse(Control card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (_previewOverflowCards.Contains(card))
        {
            card.ZIndex = GenerationPreviewExpansionVisualMetrics.CollapsingZIndex;
        }
    }

    internal void DisablePreviewOverflow(Control card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!_previewOverflowCards.Remove(card))
        {
            return;
        }

        if (_previewOriginalZIndices.Remove(card, out int originalZIndex))
        {
            card.ZIndex = originalZIndex;
        }

        if (_previewOverflowPaths.Remove(card, out IReadOnlyList<Visual>? overflowPath))
        {
            foreach (Visual visual in overflowPath)
            {
                RestoreVisualClipping(visual);
            }
        }

    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        AttachPreviewKeyboardHandlers();
        _sceneController.EnsureScene();
        EnsureResizeController();
        ResizeController.Attach();
        _itemsSubscription.ReplaceSource(Items);
        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _isAttached = false;
        DetachPreviewKeyboardHandlers();
        ResizeController.CancelResizeAnimation();
        ResizeController.Detach();
        _itemsSubscription.Clear();
        _previewPointerPosition = null;
        _previewPointerModifiers = KeyModifiers.None;
        ResetPreviewOverflow();
        _sceneController.DetachScene();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            HandleItemsChanged();
            return;
        }

        if (change.Property == OperationsProperty)
        {
            HandleOperationsChanged();
            return;
        }

        if (IsCommandProperty(change.Property))
        {
            _sceneController.UpdateCardCommands();
            return;
        }

        if (change.Property == BoundsProperty)
        {
            UpdateGalleryOpacityMask();
            ResizeController.Schedule();
        }
    }

    private void HandleItemsChanged()
    {
        if (_isAttached)
        {
            _itemsSubscription.ReplaceSource(Items);
        }
        else
        {
            _itemsSubscription.Clear();
        }

        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    private void HandleOperationsChanged()
    {
        _sceneController.DetachSceneOperations();
        _sceneController.RegisterSceneOperations();
    }

    private static bool IsCommandProperty(AvaloniaProperty property)
    {
        return (property == RevealInFolderCommandProperty)
               || (property == OpenViewerCommandProperty)
               || (property == OpenMetadataCommandProperty)
               || (property == DeleteOrCancelCommandProperty);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if ((Operations is not null) && (_sceneController.Scene is not null))
        {
            return;
        }

        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    private void EnsureResizeController()
    {
        if (_resizeController is not null)
        {
            return;
        }

        _sceneController.EnsureScene();
        AnimatedGalleryScene scene = AnimatedGallerySceneController.RequireScene(
            _sceneController.Scene);
        _resizeController = new AnimatedGalleryResizeController(
            this,
            GalleryScrollViewer,
            _sceneController,
            () => _isAttached,
            scene.ResizeLogger);
    }

    private void CancelResizeAnimation()
    {
        _resizeController?.CancelResizeAnimation();
    }

    private void UpdateGalleryOpacityMask()
    {
        if (GalleryScrollViewer.OpacityMask is not LinearGradientBrush opacityMask)
        {
            return;
        }

        if (opacityMask.GradientStops.Count < 2)
        {
            return;
        }

        double height = GalleryScrollViewer.Bounds.Height;
        if (height <= 0d)
        {
            return;
        }

        opacityMask.GradientStops[1].Offset = CalculateBottomFadeStartOffset(height);
    }

    private void ResetPreviewOverflow()
    {
        foreach ((Control card, int originalZIndex) in _previewOriginalZIndices)
        {
            card.ZIndex = originalZIndex;
        }

        _previewOriginalZIndices.Clear();
        _previewOverflowPaths.Clear();
        _previewOverflowCards.Clear();

        foreach ((Visual visual, PreviewClipState state) in _previewClipStates)
        {
            visual.ClipToBounds = state.ClipToBounds;
            visual.Clip = state.Clip;
        }

        _previewClipStates.Clear();
    }

    private IReadOnlyList<Visual> GetPreviewOverflowPath(Visual preview)
    {
        List<Visual> path = [];
        Visual? current = preview;

        while (current is not null)
        {
            path.Add(current);

            Visual? parent = current.GetVisualParent();
            if (ReferenceEquals(parent, GalleryScrollViewer))
            {
                return path;
            }

            current = parent;
        }

        throw new InvalidOperationException("Generation preview is not inside the gallery scroll viewer.");
    }

    private void DisableVisualClipping(Visual visual)
    {
        if (_previewClipStates.TryGetValue(visual, out PreviewClipState? existingState))
        {
            existingState.ReferenceCount++;
            return;
        }

        PreviewClipState state = new(visual.ClipToBounds, visual.Clip);
        _previewClipStates.Add(visual, state);
        visual.ClipToBounds = false;
        visual.Clip = null;
    }

    private void RestoreVisualClipping(Visual visual)
    {
        if (!_previewClipStates.TryGetValue(visual, out PreviewClipState? state))
        {
            return;
        }

        state.ReferenceCount--;

        if (state.ReferenceCount > 0)
        {
            return;
        }

        visual.ClipToBounds = state.ClipToBounds;
        visual.Clip = state.Clip;
        _previewClipStates.Remove(visual);
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;

        _previewPointerPosition = e.GetPosition(GalleryScrollViewer);
        _previewPointerModifiers = e.KeyModifiers;
    }

    private void OnPreviewPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        _previewPointerPosition = null;
        _previewPointerModifiers = KeyModifiers.None;
        NotifyPreviewPointerStateChanged();
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isPreviewPointerRefreshPending)
        {
            return;
        }

        _isPreviewPointerRefreshPending = true;
        GalleryScrollViewer.Dispatcher.Post(
            NotifyPreviewPointerStateChanged,
            DispatcherPriority.Loaded);
    }

    private void NotifyPreviewPointerStateChanged()
    {
        _isPreviewPointerRefreshPending = false;

        if (!_isAttached)
        {
            return;
        }

        PreviewPointerStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachPreviewKeyboardHandlers()
    {
        DetachPreviewKeyboardHandlers();
        _previewTopLevel = TopLevel.GetTopLevel(this);
        _previewTopLevel?.AddHandler(
            KeyDownEvent,
            OnPreviewKeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _previewTopLevel?.AddHandler(
            KeyUpEvent,
            OnPreviewKeyUp,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
    }

    private void DetachPreviewKeyboardHandlers()
    {
        if (_previewTopLevel is null)
        {
            return;
        }

        _previewTopLevel.RemoveHandler(KeyDownEvent, OnPreviewKeyDown);
        _previewTopLevel.RemoveHandler(KeyUpEvent, OnPreviewKeyUp);
        _previewTopLevel = null;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdatePreviewPointerModifiers(e, PreviewKeyTransition.Down);
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        _ = sender;
        UpdatePreviewPointerModifiers(e, PreviewKeyTransition.Up);
    }

    private void UpdatePreviewPointerModifiers(
        KeyEventArgs e,
        PreviewKeyTransition transition)
    {
        KeyModifiers modifier = GenerationPreviewExpansionController.GetExpansionModifier(e.Key);

        if (modifier == KeyModifiers.None)
        {
            return;
        }

        _previewPointerModifiers = transition switch
        {
            PreviewKeyTransition.Down => e.KeyModifiers | modifier,
            PreviewKeyTransition.Up => e.KeyModifiers & ~modifier,
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null)
        };
        NotifyPreviewPointerStateChanged();
    }

    private enum PreviewKeyTransition
    {
        Down,
        Up
    }

    private sealed class PreviewClipState
    {
        public bool ClipToBounds { get; }
        public Geometry? Clip { get; }
        public int ReferenceCount { get; set; } = 1;

        public PreviewClipState(bool clipToBounds, Geometry? clip)
        {
            ClipToBounds = clipToBounds;
            Clip = clip;
        }
    }
}
