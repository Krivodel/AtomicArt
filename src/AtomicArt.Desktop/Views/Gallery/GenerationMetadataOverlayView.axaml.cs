using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationMetadataOverlayView : UserControl
{
    public GenerationReferencePreviewLoader? ReferencePreviewLoader { get; }
    public IGenerationReferenceDragService? ReferenceDragService { get; }

    internal IUiFrameScheduler? NavigationFrameScheduler { get; init; }

    private const double NavigationTriggerWidthMultiplier = 4d;

    private ColumnDefinition PreviousNavigationColumn => NavigationLayout.ColumnDefinitions[0];
    private ColumnDefinition NextNavigationColumn => NavigationLayout.ColumnDefinitions[^1];

    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;
    private AvaloniaUiFrameScheduler? _ownedNavigationFrameScheduler;
    private GenerationMetadataNavigationTransition? _navigationTransition;
    private GenerationMetadataViewModel? _observedMetadata;

    public GenerationMetadataOverlayView()
    {
        InitializeComponent();
        PromptCopyButton.PropertyChanged += OnPromptCopyButtonPropertyChanged;
        NavigationLayout.MaxWidth = PanelRoot.MaxWidth
            + NavigationTriggerWidthMultiplier * (PreviousNavigationColumn.MinWidth + NextNavigationColumn.MinWidth);
        _previewExpansionHost = new StandaloneGenerationPreviewExpansionHost(this);
        PreviewEntry.ExpansionHost = _previewExpansionHost;
        PreviewEntry.OverflowOwner = PreviewEntry;
    }

    public GenerationMetadataOverlayView(
        IGenerationPreviewSessionFactory previewSessionFactory,
        GenerationReferencePreviewLoader? referencePreviewLoader = null,
        IGenerationReferenceDragService? referenceDragService = null)
        : this()
    {
        _previewSessionFactory = previewSessionFactory
            ?? throw new ArgumentNullException(nameof(previewSessionFactory));
        ReferencePreviewLoader = referencePreviewLoader;
        ReferenceDragService = referenceDragService;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _previewExpansionHost.Attach();

        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            AttachPreviewSession(topLevel);
            IUiFrameScheduler frameScheduler = NavigationFrameScheduler
                ?? (_ownedNavigationFrameScheduler = new AvaloniaUiFrameScheduler(topLevel));
            _navigationTransition = new GenerationMetadataNavigationTransition(
                NavigationLayout,
                PanelRoot,
                MetadataBody,
                PreviewEntry,
                PreviousMetadataPage,
                new UiAnimationScheduler(frameScheduler));
            ObserveMetadata(DataContext as GenerationMetadataViewModel);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ObserveMetadata(null);
        _navigationTransition = null;
        _ownedNavigationFrameScheduler?.Dispose();
        _ownedNavigationFrameScheduler = null;
        _previewSession?.Dispose();
        _previewSession = null;
        _previewExpansionHost.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_navigationTransition is not null)
        {
            ObserveMetadata(DataContext as GenerationMetadataViewModel);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double layoutWidth = Math.Min(availableSize.Width, NavigationLayout.MaxWidth);
        double sideWidth = Math.Max(PreviousNavigationColumn.MinWidth, (layoutWidth - PanelRoot.MaxWidth) / 2d);
        PreviousNavigationColumn.Width = NextNavigationColumn.Width = new GridLength(sideWidth);
        return base.MeasureOverride(availableSize);
    }

    private void AttachPreviewSession(TopLevel topLevel)
    {
        IGenerationPreviewSessionFactory? previewSessionFactory =
            _previewSessionFactory;

        if (previewSessionFactory is null || _previewSession is not null)
        {
            return;
        }

        IGenerationPreviewSession previewSession =
            previewSessionFactory.Create(topLevel);

        try
        {
            previewSession.Attach(PreviewEntry);
            _previewSession = previewSession;
        }
        catch (Exception)
        {
            previewSession.Dispose();
            throw;
        }
    }

    private void ObserveMetadata(GenerationMetadataViewModel? metadata)
    {
        if (ReferenceEquals(_observedMetadata, metadata))
        {
            return;
        }

        if (_observedMetadata is not null)
        {
            _observedMetadata.PropertyChanging -= OnMetadataPropertyChanging;
            _observedMetadata.ResetPromptCopyFeedback();
        }

        _observedMetadata = metadata;
        if (metadata is not null)
        {
            metadata.PropertyChanging += OnMetadataPropertyChanging;
        }

        _navigationTransition?.Reset();
    }

    private void OnPromptCopyButtonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == OpacityProperty && PromptCopyButton.Opacity == 0d
            && DataContext is GenerationMetadataViewModel metadata)
        {
            metadata.ResetPromptCopyFeedback();
        }
    }

    private void OnMetadataPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationMetadataViewModel.Item))
        {
            _navigationTransition?.Start();
        }
    }
}
