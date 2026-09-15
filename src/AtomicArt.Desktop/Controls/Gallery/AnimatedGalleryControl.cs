using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

public partial class AnimatedGalleryControl : UserControl
{
    public IEnumerable<IGalleryItemViewModel>? Items
    {
        get => GetValue(AnimatedGalleryControl.ItemsProperty);
        set => SetValue(AnimatedGalleryControl.ItemsProperty, value);
    }
    public IRelayCommand? RevealInFolderCommand
    {
        get => GetValue(AnimatedGalleryControl.RevealInFolderCommandProperty);
        set => SetValue(AnimatedGalleryControl.RevealInFolderCommandProperty, value);
    }
    public IRelayCommand? RevealInNewFolderWindowCommand
    {
        get => GetValue(AnimatedGalleryControl.RevealInNewFolderWindowCommandProperty);
        set => SetValue(AnimatedGalleryControl.RevealInNewFolderWindowCommandProperty, value);
    }
    public IRelayCommand? OpenViewerCommand
    {
        get => GetValue(AnimatedGalleryControl.OpenViewerCommandProperty);
        set => SetValue(AnimatedGalleryControl.OpenViewerCommandProperty, value);
    }
    public IRelayCommand? ShowFailureDetailsCommand
    {
        get => GetValue(AnimatedGalleryControl.ShowFailureDetailsCommandProperty);
        set => SetValue(AnimatedGalleryControl.ShowFailureDetailsCommandProperty, value);
    }
    public IRelayCommand? OpenMetadataCommand
    {
        get => GetValue(AnimatedGalleryControl.OpenMetadataCommandProperty);
        set => SetValue(AnimatedGalleryControl.OpenMetadataCommandProperty, value);
    }
    public IRelayCommand? DeleteOrCancelCommand
    {
        get => GetValue(AnimatedGalleryControl.DeleteOrCancelCommandProperty);
        set => SetValue(AnimatedGalleryControl.DeleteOrCancelCommandProperty, value);
    }
    public IRelayCommand? ToggleFavoriteCommand
    {
        get => GetValue(AnimatedGalleryControl.ToggleFavoriteCommandProperty);
        set => SetValue(AnimatedGalleryControl.ToggleFavoriteCommandProperty, value);
    }
    public IRelayCommand? OpenDlss5Command
    {
        get => GetValue(AnimatedGalleryControl.OpenDlss5CommandProperty);
        set => SetValue(AnimatedGalleryControl.OpenDlss5CommandProperty, value);
    }
    public IRelayCommand? ToggleSelectionCommand
    {
        get => GetValue(AnimatedGalleryControl.ToggleSelectionCommandProperty);
        set => SetValue(AnimatedGalleryControl.ToggleSelectionCommandProperty, value);
    }
    public IRelayCommand? SelectRangeCommand
    {
        get => GetValue(AnimatedGalleryControl.SelectRangeCommandProperty);
        set => SetValue(AnimatedGalleryControl.SelectRangeCommandProperty, value);
    }
    public bool IsSelectionMode
    {
        get => GetValue(AnimatedGalleryControl.IsSelectionModeProperty);
        set => SetValue(AnimatedGalleryControl.IsSelectionModeProperty, value);
    }
    public IAnimatedGalleryOperations? Operations
    {
        get => GetValue(AnimatedGalleryControl.OperationsProperty);
        set => SetValue(AnimatedGalleryControl.OperationsProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<IGalleryItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IEnumerable<IGalleryItemViewModel>?>(
            nameof(Items));
    public static readonly StyledProperty<IRelayCommand?> RevealInFolderCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(RevealInFolderCommand));
    public static readonly StyledProperty<IRelayCommand?> RevealInNewFolderWindowCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(RevealInNewFolderWindowCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenViewerCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(OpenViewerCommand));
    public static readonly StyledProperty<IRelayCommand?> ShowFailureDetailsCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(ShowFailureDetailsCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenMetadataCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(OpenMetadataCommand));
    public static readonly StyledProperty<IRelayCommand?> DeleteOrCancelCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(DeleteOrCancelCommand));
    public static readonly StyledProperty<IRelayCommand?> ToggleFavoriteCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(ToggleFavoriteCommand));
    public static readonly StyledProperty<IRelayCommand?> OpenDlss5CommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(OpenDlss5Command));
    public static readonly StyledProperty<IRelayCommand?> ToggleSelectionCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(ToggleSelectionCommand));
    public static readonly StyledProperty<IRelayCommand?> SelectRangeCommandProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IRelayCommand?>(
            nameof(SelectRangeCommand));
    public static readonly StyledProperty<bool> IsSelectionModeProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, bool>(
            nameof(IsSelectionMode));
    public static readonly StyledProperty<IAnimatedGalleryOperations?> OperationsProperty =
        AvaloniaProperty.Register<AnimatedGalleryControl, IAnimatedGalleryOperations?>(
            nameof(Operations));

    internal const double BottomOpacityFadeHeight = 30d;

    internal ScrollViewer PreviewScrollViewer => GalleryScrollViewer;
    internal IGenerationPreviewExpansionHost PreviewExpansionHost { get; }

    internal event EventHandler? PreviewPointerStateChanged;
    internal event EventHandler? PreviewModifiersChanged;

    private const int OpacityMaskGradientStopCount = 2;
    private const int BottomOpacityMaskGradientStopIndex = 1;

    private AnimatedGalleryResizeController ResizeController =>
        _resizeController ?? throw new InvalidOperationException("Animated gallery resize controller was not created.");

    private readonly AnimatedGallerySceneController _sceneController;
    private readonly AnimatedGallerySelectionBrushController _selectionBrushController;
    private readonly CollectionChangedSubscription _itemsSubscription;
    private readonly PropertyChangedItemsSubscription<IGalleryItemViewModel> _itemPropertyChangedSubscription;
    private readonly Dictionary<Control, int> _previewOriginalZIndices = [];
    private readonly Dictionary<Control, IReadOnlyList<Visual>> _previewOverflowPaths = [];
    private readonly Dictionary<Visual, PreviewClipState> _previewClipStates = [];
    private readonly HashSet<Control> _previewOverflowCards = [];
    private AnimatedGalleryResizeController? _resizeController;
    private TopLevel? _previewTopLevel;
    private Point? _previewPointerPosition;
    private KeyModifiers _previewPointerModifiers;
    private bool _isPreviewPointerRefreshPending;
    private bool _isSelectionVisualRefreshPending;
    private bool _isAttached;

    public AnimatedGalleryControl()
        : this(null)
    {
    }

    internal AnimatedGalleryControl(IAnimatedGallerySceneFactory? sceneFactory)
    {
        _itemsSubscription = new CollectionChangedSubscription(OnItemsCollectionChanged);
        _itemPropertyChangedSubscription =
            new PropertyChangedItemsSubscription<IGalleryItemViewModel>(
                OnGalleryItemPropertyChanged);
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
        _selectionBrushController =
            new AnimatedGallerySelectionBrushController(this);
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

    internal static double CalculateBottomFadeStartOffset(double height)
    {
        if (height <= BottomOpacityFadeHeight)
        {
            return 0d;
        }

        return (height - BottomOpacityFadeHeight) / height;
    }

    internal Guid GetItemId(object item)
    {
        if (item is IGalleryItemViewModel galleryItem)
        {
            return galleryItem.Id;
        }

        throw new InvalidOperationException($"Gallery item '{item.GetType().Name}' does not expose a supported identifier.");
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);

        _isAttached = true;
        AttachPreviewKeyboardHandlers();
        _sceneController.EnsureScene();
        EnsureResizeController();
        ResizeController.Attach();
        _itemsSubscription.ReplaceSource(Items);
        _itemPropertyChangedSubscription.ReplaceSources(Items);
        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnDetachedFromVisualTree(eventArgs);

        _isAttached = false;
        DetachPreviewKeyboardHandlers();
        ResizeController.CancelResizeAnimation();
        ResizeController.Detach();
        _itemsSubscription.Clear();
        _itemPropertyChangedSubscription.Clear();
        _isSelectionVisualRefreshPending = false;
        _previewPointerPosition = null;
        _previewPointerModifiers = KeyModifiers.None;
        _selectionBrushController.Cancel();
        ResetPreviewOverflow();
        _sceneController.DetachScene();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AnimatedGalleryControl.ItemsProperty)
        {
            HandleItemsChanged();
            return;
        }

        if (change.Property == AnimatedGalleryControl.OperationsProperty)
        {
            HandleOperationsChanged();
            return;
        }

        if (AnimatedGalleryControl.IsCommandProperty(change.Property))
        {
            _sceneController.UpdateCardCommands();
            return;
        }

        if (change.Property == AnimatedGalleryControl.IsSelectionModeProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                _selectionBrushController.HandleSelectionModeEnded();
            }

            _sceneController.UpdateCardSelectionMode();
            ScheduleSelectionVisualRefresh();
            RaisePreviewPointerStateChanged();
            return;
        }

        if (change.Property == BoundsProperty)
        {
            UpdateGalleryOpacityMask();
            ResizeController.Schedule();
        }
    }

    private static bool IsCommandProperty(AvaloniaProperty property)
    {
        return (property == AnimatedGalleryControl.RevealInFolderCommandProperty)
               || (property == AnimatedGalleryControl.RevealInNewFolderWindowCommandProperty)
               || (property == AnimatedGalleryControl.OpenViewerCommandProperty)
               || (property == AnimatedGalleryControl.ShowFailureDetailsCommandProperty)
               || (property == AnimatedGalleryControl.OpenMetadataCommandProperty)
               || (property == AnimatedGalleryControl.DeleteOrCancelCommandProperty)
               || (property == AnimatedGalleryControl.ToggleFavoriteCommandProperty)
               || (property == AnimatedGalleryControl.OpenDlss5CommandProperty)
               || (property == AnimatedGalleryControl.ToggleSelectionCommandProperty)
               || (property == AnimatedGalleryControl.SelectRangeCommandProperty);
    }

    private void HandleItemsChanged()
    {
        if (_isAttached)
        {
            _itemsSubscription.ReplaceSource(Items);
            _itemPropertyChangedSubscription.ReplaceSources(Items);
        }
        else
        {
            _itemsSubscription.Clear();
            _itemPropertyChangedSubscription.Clear();
        }

        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    private void HandleOperationsChanged()
    {
        _sceneController.DetachSceneOperations();
        _sceneController.RegisterSceneOperations();
    }

    private void ScheduleSelectionVisualRefresh()
    {
        if ((!_isAttached) || (_isSelectionVisualRefreshPending))
        {
            return;
        }

        _isSelectionVisualRefreshPending = true;
        Dispatcher.Post(
            RefreshSelectionVisual,
            DispatcherPriority.Render);
    }

    private void RefreshSelectionVisual()
    {
        _isSelectionVisualRefreshPending = false;

        if (_isAttached)
        {
            _sceneController.UpdateSelectionDimming();
        }
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

        if (opacityMask.GradientStops.Count < OpacityMaskGradientStopCount)
        {
            return;
        }

        double height = GalleryScrollViewer.Bounds.Height;
        if (height <= 0d)
        {
            return;
        }

        opacityMask.GradientStops[BottomOpacityMaskGradientStopIndex].Offset =
            AnimatedGalleryControl.CalculateBottomFadeStartOffset(height);
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

    private void SchedulePreviewPointerStateChanged()
    {
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
        RaisePreviewPointerStateChanged();
    }

    private void RaisePreviewPointerStateChanged()
    {
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

    private void UpdatePreviewPointerModifiers(
        KeyEventArgs eventArgs,
        PreviewKeyTransition transition)
    {
        KeyModifiers modifier = GenerationPreviewExpansionController.GetExpansionModifier(eventArgs.Key);

        if (modifier == KeyModifiers.None)
        {
            return;
        }

        _previewPointerModifiers = transition switch
        {
            PreviewKeyTransition.Down => eventArgs.KeyModifiers | modifier,
            PreviewKeyTransition.Up => eventArgs.KeyModifiers & ~modifier,
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null)
        };
        RaisePreviewPointerStateChanged();
        PreviewModifiersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _itemPropertyChangedSubscription.ReplaceSources(Items);
        ScheduleSelectionVisualRefresh();

        if ((Operations is not null) && (_sceneController.Scene is not null))
        {
            return;
        }

        _sceneController.RefreshItems();
        _sceneController.RefreshScene();
    }

    private void OnGalleryItemPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if ((!string.IsNullOrEmpty(eventArgs.PropertyName))
            && (!string.Equals(
                eventArgs.PropertyName,
                nameof(IGalleryItemViewModel.IsSelected),
                StringComparison.Ordinal)))
        {
            return;
        }

        ScheduleSelectionVisualRefresh();
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        _previewPointerPosition = eventArgs.GetPosition(GalleryScrollViewer);
        _previewPointerModifiers = eventArgs.KeyModifiers;

        if (GenerationPreviewExpansionController.HasExpansionModifier(
                _previewPointerModifiers))
        {
            SchedulePreviewPointerStateChanged();
        }
    }

    private void OnPreviewPointerExited(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _previewPointerPosition = null;
        _previewPointerModifiers = KeyModifiers.None;
        NotifyPreviewPointerStateChanged();
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        SchedulePreviewPointerStateChanged();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;
        UpdatePreviewPointerModifiers(eventArgs, PreviewKeyTransition.Down);
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;
        UpdatePreviewPointerModifiers(eventArgs, PreviewKeyTransition.Up);
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
