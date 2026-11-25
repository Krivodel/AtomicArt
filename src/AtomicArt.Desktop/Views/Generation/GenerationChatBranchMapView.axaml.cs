using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationChatBranchMapView : UserControl
{
    private const double PreviewRetentionRatio = 2d;
    private const int QualityUpdateDelayMilliseconds = 300;

    private readonly BitmapCache _cardCache = new();
    private readonly DispatcherTimer _qualityUpdate = new()
    {
        Interval = TimeSpan.FromMilliseconds(QualityUpdateDelayMilliseconds)
    };
    private readonly HashSet<GenerationPreviewControl> _previews = [];
    private readonly Dictionary<GenerationPreviewControl, (GenerationItemViewModel Card, Control Host)> _observedPreviews = [];
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;

    public GenerationChatBranchMapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _qualityUpdate.Tick += OnQualityUpdate;
        MapViewport.PropertyChanged += OnCameraPropertyChanged;
        MapViewport.CameraChanged += OnCameraChanged;
    }

    public GenerationChatBranchMapView(IGenerationPreviewSessionFactory previewSessionFactory) : this()
    {
        _previewSessionFactory = previewSessionFactory ?? throw new ArgumentNullException(nameof(previewSessionFactory));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _qualityUpdate.Stop();
        _previewSession?.Dispose();
        _previewSession = null;
        _previews.Clear();

        foreach (GenerationItemViewModel card in _observedPreviews.Values.Select(preview => preview.Card))
        {
            card.PropertyChanged -= OnPreviewCardChanged;
        }

        _observedPreviews.Clear();
        base.OnDetachedFromVisualTree(e);
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

    private void UpdateCardCache(GenerationPreviewControl preview)
    {
        if (preview.GetVisualAncestors().OfType<Button>().FirstOrDefault() is { } button
            && preview.DataContext is GenerationItemViewModel card)
        {
            button.CacheMode = card.IsGenerating ? null : _cardCache;
        }
    }

    private void UpdateVisiblePreviews()
    {
        (Rect load, Rect retain) = GetPreviewBounds();

        foreach (KeyValuePair<GenerationPreviewControl, (GenerationItemViewModel Card, Control Host)> preview in _observedPreviews)
        {
            UpdatePreviewVisibility(preview.Key, preview.Value.Host, load, retain);
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
        if (host.Bounds.Intersects(load))
        {
            AttachPreview(preview);
        }
        else if (!host.Bounds.Intersects(retain))
        {
            DetachPreview(preview);
        }
    }

    private void OnPreviewCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationItemViewModel.IsGenerating))
        {
            foreach (GenerationPreviewControl preview in _observedPreviews.Where(pair => ReferenceEquals(pair.Value.Card, sender)).Select(pair => pair.Key))
            {
                UpdateCardCache(preview);
            }
        }
    }

    private void OnPreviewLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is GenerationPreviewControl loaded && loaded.DataContext is GenerationItemViewModel card
            && loaded.GetVisualAncestors().OfType<Control>().FirstOrDefault(control => control.GetVisualParent() is GenerationChatBranchPanel) is { } host)
        {
            if (_observedPreviews.TryAdd(loaded, (card, host)))
            {
                card.PropertyChanged += OnPreviewCardChanged;
            }

            foreach (AttachmentPixelLoadingControl indicator in loaded.GetVisualDescendants().OfType<AttachmentPixelLoadingControl>())
            {
                indicator.CacheFrames = true;
            }

            UpdateCardCache(loaded);

            (Rect load, Rect retain) = GetPreviewBounds();
            UpdatePreviewVisibility(loaded, host, load, retain);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateVisiblePreviews();
        MapViewport.Focus();
    }

    private void OnPreviewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is GenerationPreviewControl preview)
        {
            if (_observedPreviews.Remove(preview, out (GenerationItemViewModel Card, Control Host) observed))
            {
                observed.Card.PropertyChanged -= OnPreviewCardChanged;
            }

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

        _cardCache.RenderAtScale = Math.Max(1d, Math.Ceiling(MapViewport.Zoom));
    }

    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button { DataContext: GenerationChatBranchNodeViewModel node }
            && DataContext is GenerationChatBranchMapViewModel map && map.OpenNodeCommand.CanExecute(node))
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
