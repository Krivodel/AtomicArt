using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationMetadataOverlayView : UserControl
{
    public GenerationReferencePreviewLoader? ReferencePreviewLoader { get; }
    public IGenerationReferenceDragService? ReferenceDragService { get; }

    private const double NavigationTriggerWidthMultiplier = 4d;

    private ColumnDefinition PreviousNavigationColumn => NavigationLayout.ColumnDefinitions[0];
    private ColumnDefinition NextNavigationColumn => NavigationLayout.ColumnDefinitions[^1];

    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;

    public GenerationMetadataOverlayView()
    {
        InitializeComponent();
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
        AttachPreviewSession();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _previewSession?.Dispose();
        _previewSession = null;
        _previewExpansionHost.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double layoutWidth = Math.Min(availableSize.Width, NavigationLayout.MaxWidth);
        double sideWidth = Math.Max(PreviousNavigationColumn.MinWidth, (layoutWidth - PanelRoot.MaxWidth) / 2d);
        PreviousNavigationColumn.Width = NextNavigationColumn.Width = new GridLength(sideWidth);
        return base.MeasureOverride(availableSize);
    }

    private void AttachPreviewSession()
    {
        IGenerationPreviewSessionFactory? previewSessionFactory =
            _previewSessionFactory;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        if (previewSessionFactory is null
            || topLevel is null
            || _previewSession is not null)
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
}
