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
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationChatBranchMapView : UserControl
{
    public BitmapCache CardCache { get; } = new();

    private const double PreviewRetentionRatio = 2d;
    private const int QualityUpdateDelayMilliseconds = 300;
    private const int DeletedFeedbackDurationMilliseconds = 600;

    private static readonly MotionFrame[] DeletedFeedbackFrames =
    [
        new MotionFrame(0d, 0d, 1d, 0d, 0d),
        new MotionFrame(0d, 0d, 1d, 0d, 1d),
        new MotionFrame(0d, 0d, 1d, 0d, 0d),
        new MotionFrame(0d, 0d, 1d, 0d, 1d),
        new MotionFrame(0d, 0d, 1d, 0d, 0d)
    ];
    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;
    private readonly DispatcherTimer _qualityUpdate = new()
    {
        Interval = TimeSpan.FromMilliseconds(QualityUpdateDelayMilliseconds)
    };
    private readonly HashSet<GenerationPreviewControl> _previews = [];
    private readonly Dictionary<GenerationPreviewControl, Control> _observedPreviews = [];
    private readonly Dictionary<Border, Button> _feedbackNodes = [];
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;
    private AvaloniaUiFrameScheduler? _feedbackFrames;
    private UiAnimationScheduler? _feedbackAnimator;

    public GenerationChatBranchMapView()
    {
        InitializeComponent();
        _previewExpansionHost = new StandaloneGenerationPreviewExpansionHost(MapViewport, PreviewLayer);
        Loaded += OnLoaded;
        _qualityUpdate.Tick += OnQualityUpdate;
        MapViewport.PropertyChanged += OnCameraPropertyChanged;
        MapViewport.CameraChanged += OnCameraChanged;
    }

    public GenerationChatBranchMapView(IGenerationPreviewSessionFactory previewSessionFactory) : this()
    {
        _previewSessionFactory = previewSessionFactory ?? throw new ArgumentNullException(nameof(previewSessionFactory));
    }

    internal GenerationChatBranchMapView(
        IGenerationPreviewSessionFactory previewSessionFactory, IUiFrameScheduler frameScheduler)
        : this(previewSessionFactory)
    {
        _feedbackAnimator = new UiAnimationScheduler(frameScheduler);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _qualityUpdate.Stop();
        _previewSession?.Dispose();
        _previewSession = null;
        _previews.Clear();
        _previewExpansionHost.Detach();
        _observedPreviews.Clear();

        foreach (KeyValuePair<Border, Button> node in _feedbackNodes)
        {
            _feedbackAnimator?.Cancel(node.Key);
            node.Key.Opacity = 0d;
            node.Value.Classes.Remove("feedback");
        }

        _feedbackNodes.Clear();
        _feedbackFrames?.Dispose();
        _feedbackFrames = null;
        _feedbackAnimator = null;
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

        if (_feedbackAnimator is null)
        {
            _feedbackFrames = new AvaloniaUiFrameScheduler(topLevel);
            _feedbackAnimator = new UiAnimationScheduler(_feedbackFrames);
        }

        _feedbackAnimator.Cancel(border);
        _feedbackNodes[border] = button;
        button.Classes.Set("feedback", true);
        _ = _feedbackAnimator.AnimateAsync(border, DeletedFeedbackFrames, DeletedFeedbackDurationMilliseconds,
            0, MotionEasing.Linear, () =>
            {
                _feedbackNodes.Remove(border);
                button.Classes.Remove("feedback");
            });
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
        _previewExpansionHost.Attach();

        if (BranchItems.ItemsPanelRoot is GenerationChatBranchPanel panel
            && panel.Children.FirstOrDefault(child => child.DataContext is GenerationChatBranchNodeViewModel { IsCurrent: true }) is { } current)
        {
            MapViewport.FocusOn(current.Bounds);
        }

        UpdateVisiblePreviews();
        MapViewport.Focus();
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
