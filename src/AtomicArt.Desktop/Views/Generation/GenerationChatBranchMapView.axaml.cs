using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationChatBranchMapView : UserControl
{
    public BitmapCache CardCache { get; } = new();

    private const double PreviewRetentionRatio = 2d;
    private const int QualityUpdateDelayMilliseconds = 300;
    private const int DeletedFeedbackDurationMilliseconds = 600;
    private const double ArrivalOffsetY = 24d;
    private const double ArrivalScale = 0.96d;

    private static readonly MotionFrame[] DeletedFeedbackFrames =
    [
        new MotionFrame(0d, 0d, 1d, 0d, 0d),
        new MotionFrame(0d, 0d, 1d, 0d, 1d),
        new MotionFrame(0d, 0d, 1d, 0d, 0d),
        new MotionFrame(0d, 0d, 1d, 0d, 1d),
        new MotionFrame(0d, 0d, 1d, 0d, 0d)
    ];
    private static readonly MotionFrame[] ArrivalFrames =
    [
        new MotionFrame(0d, ArrivalOffsetY, ArrivalScale, 0d, 0d),
        MotionFrame.Identity
    ];
    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;
    private readonly DispatcherTimer _qualityUpdate = new()
    {
        Interval = TimeSpan.FromMilliseconds(QualityUpdateDelayMilliseconds)
    };
    private readonly HashSet<GenerationPreviewControl> _previews = [];
    private readonly Dictionary<GenerationPreviewControl, Control> _observedPreviews = [];
    private readonly Dictionary<Border, Button> _feedbackNodes = [];
    private readonly HashSet<GenerationItemViewModel> _knownCards = [];
    private readonly Dictionary<Button, Control> _arrivingNodes = [];
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;
    private AvaloniaUiFrameScheduler? _frames;
    private UiAnimationScheduler? _animation;
    private bool _isMapLoaded;

    public GenerationChatBranchMapView()
    {
        InitializeComponent();
        _previewExpansionHost = new StandaloneGenerationPreviewExpansionHost(MapViewport, PreviewLayer);
        Loaded += OnLoaded;
        _qualityUpdate.Tick += OnQualityUpdate;
        MapViewport.PropertyChanged += OnCameraPropertyChanged;
        MapViewport.CameraChanged += OnCameraChanged;
        MapViewport.BackgroundClicked += OnBackgroundClicked;
    }

    public GenerationChatBranchMapView(IGenerationPreviewSessionFactory previewSessionFactory) : this()
    {
        _previewSessionFactory = previewSessionFactory ?? throw new ArgumentNullException(nameof(previewSessionFactory));
    }

    internal GenerationChatBranchMapView(
        IGenerationPreviewSessionFactory previewSessionFactory, IUiFrameScheduler frameScheduler)
        : this(previewSessionFactory)
    {
        _animation = new UiAnimationScheduler(frameScheduler);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RememberCards();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RememberCards();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isMapLoaded = false;
        _knownCards.Clear();

        foreach (Control target in _arrivingNodes.Values)
        {
            _animation?.Cancel(target);
            MotionFrameApplier.Apply(target, MotionFrame.Identity);
        }

        _arrivingNodes.Clear();
        _qualityUpdate.Stop();
        _previewSession?.Dispose();
        _previewSession = null;
        _previews.Clear();
        _previewExpansionHost.Detach();
        _observedPreviews.Clear();

        foreach (KeyValuePair<Border, Button> node in _feedbackNodes)
        {
            _animation?.Cancel(node.Key);
            node.Key.Opacity = 0d;
            node.Value.Classes.Remove("feedback");
        }

        _feedbackNodes.Clear();
        _frames?.Dispose();
        _frames = null;
        _animation = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ShowDeletedFeedback(Button button)
    {
        if (button.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("generation-chat-branch-feedback")) is not { } border
            || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        UiAnimationScheduler animation = GetAnimation(topLevel);

        animation.Cancel(border);
        _feedbackNodes[border] = button;
        button.Classes.Set("feedback", true);
        _ = animation.AnimateAsync(border, DeletedFeedbackFrames, DeletedFeedbackDurationMilliseconds,
            0, MotionEasing.Linear, () =>
            {
                _feedbackNodes.Remove(border);
                button.Classes.Remove("feedback");
            });
    }

    private UiAnimationScheduler GetAnimation(TopLevel topLevel)
    {
        if (_animation is null)
        {
            _frames = new AvaloniaUiFrameScheduler(topLevel);
            _animation = new UiAnimationScheduler(_frames);
        }

        return _animation;
    }

    private void RememberCards()
    {
        _knownCards.Clear();

        if (DataContext is GenerationChatBranchMapViewModel map)
        {
            foreach (GenerationChatBranchNodeViewModel node in map.Nodes)
            {
                if (node.Card is { } card)
                {
                    _knownCards.Add(card);
                }
            }
        }
    }

    private void AttachPreview(GenerationPreviewControl preview)
    {
        if (_previewSessionFactory is null || _previews.Contains(preview) || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _previewSession ??= _previewSessionFactory.Create(topLevel);
        _previewSession.Attach(preview);
        _previews.Add(preview);
    }

    private void DetachPreview(GenerationPreviewControl preview)
    {
        if (_previews.Remove(preview))
        {
            _previewSession?.Detach(preview);
        }
    }

    private void UpdateVisiblePreviews()
    {
        (Rect load, Rect retain) = GetPreviewBounds();

        foreach (KeyValuePair<GenerationPreviewControl, Control> preview in _observedPreviews)
        {
            UpdatePreviewVisibility(preview.Key, preview.Value, load, retain);
        }
    }

    private (Rect Load, Rect Retain) GetPreviewBounds()
    {
        Rect visible = MapViewport.VisibleContentBounds;
        double buffer = GenerationChatBranchPanel.NodeWidth / MapViewport.Zoom;

        return (visible.Inflate(buffer), visible.Inflate(buffer * PreviewRetentionRatio));
    }

    private void UpdatePreviewVisibility(GenerationPreviewControl preview, Control host, Rect load, Rect retain)
    {
        if (host.Bounds.Intersects(load) || host.Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass))
        {
            AttachPreview(preview);
        }
        else if (!host.Bounds.Intersects(retain))
        {
            DetachPreview(preview);
        }
    }

    private void OnPreviewLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is GenerationPreviewControl loaded
            && loaded.GetVisualAncestors().OfType<Control>().FirstOrDefault(control => control.GetVisualParent() is GenerationChatBranchPanel) is { } host)
        {
            _observedPreviews.TryAdd(loaded, host);
            loaded.IsImageDraggingEnabled = false;
            loaded.OverflowOwner = host;
            loaded.ExpansionHost = _previewExpansionHost;

            foreach (AttachmentPixelLoadingControl indicator in loaded.GetVisualDescendants().OfType<AttachmentPixelLoadingControl>())
            {
                indicator.CacheFrames = true;
            }

            (Rect load, Rect retain) = GetPreviewBounds();
            UpdatePreviewVisibility(loaded, host, load, retain);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isMapLoaded = true;
        _previewExpansionHost.Attach();

        if (BranchItems.ItemsPanelRoot is GenerationChatBranchPanel panel
            && panel.Children.FirstOrDefault(child => child.DataContext is GenerationChatBranchNodeViewModel { IsCurrent: true }) is { } current)
        {
            MapViewport.FocusOn(current.Bounds);
        }

        UpdateVisiblePreviews();
        MapViewport.Focus();
    }

    private void OnNodeLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: GenerationChatBranchNodeViewModel { Card: { } card } } button
            || !_knownCards.Add(card) || !_isMapLoaded
            || button.GetVisualAncestors().OfType<Control>()
                .FirstOrDefault(control => control.GetVisualParent() is GenerationChatBranchPanel) is not { } target
            || TopLevel.GetTopLevel(this) is not { } topLevel
            || this.FindResource(GenerationChatMessageView.MessageArrivalDurationResourceKey) is not TimeSpan duration)
        {
            return;
        }

        _arrivingNodes[button] = target;
        _ = GetAnimation(topLevel).AnimateAsync(target, ArrivalFrames, (int)duration.TotalMilliseconds,
            0, MotionEasing.EaseRail, () => _arrivingNodes.Remove(button));
    }

    private void OnNodeUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && _arrivingNodes.Remove(button, out Control? target))
        {
            _animation?.Cancel(target);
            MotionFrameApplier.Apply(target, MotionFrame.Identity);
        }
    }

    private void OnPreviewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is GenerationPreviewControl preview)
        {
            _observedPreviews.Remove(preview);
            preview.ExpansionHost = null;
            DetachPreview(preview);
        }
    }

    private void OnCameraChanged(object? sender, EventArgs e)
    {
        UpdateVisiblePreviews();
    }

    private void OnBackgroundClicked(object? sender, EventArgs e)
    {
        if (DataContext is GenerationChatBranchMapViewModel map)
        {
            map.HideComposerCommand.Execute(null);
        }
    }

    private void OnCameraPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != PanZoomControl.IsInteractingProperty)
        {
            return;
        }

        _qualityUpdate.Stop();

        if (!MapViewport.IsInteracting)
        {
            _qualityUpdate.Start();
        }
    }

    private void OnQualityUpdate(object? sender, EventArgs e)
    {
        _qualityUpdate.Stop();

        if (MapViewport.IsInteracting || VisualRoot is null)
        {
            return;
        }

        CardCache.RenderAtScale = Math.Max(1d, Math.Ceiling(MapViewport.Zoom));
    }

    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<GenerationPreviewControl>()
            .Any(preview => preview.DataContext is GenerationItemViewModel { ShowsGeneratedImage: true }))
        {
            e.Handled = true;
            return;
        }

        if (sender is not Button { DataContext: GenerationChatBranchNodeViewModel node } button
            || DataContext is not GenerationChatBranchMapViewModel map)
        {
            return;
        }

        if (node.IsDeleted)
        {
            ShowDeletedFeedback(button);
            e.Handled = true;
        }
        else if (map.OpenNodeCommand.CanExecute(node))
        {
            map.OpenNodeCommand.Execute(node);
            e.Handled = true;
        }
    }

    private void OnNodePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Button { DataContext: GenerationChatBranchNodeViewModel node }
            && BranchItems.ItemsPanelRoot is GenerationChatBranchPanel panel)
        {
            panel.HoveredNodeId = node.Id;
        }
    }

    private void OnNodePointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Button { DataContext: GenerationChatBranchNodeViewModel node }
            && BranchItems.ItemsPanelRoot is GenerationChatBranchPanel panel && panel.HoveredNodeId == node.Id)
        {
            panel.HoveredNodeId = null;
        }
    }
}
