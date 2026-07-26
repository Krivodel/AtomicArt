using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationMetadataOverlayView : UserControl
{
    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;
    private readonly IGenerationPreviewSessionFactory? _previewSessionFactory;
    private IGenerationPreviewSession? _previewSession;

    public GenerationMetadataOverlayView()
    {
        InitializeComponent();
        _previewExpansionHost = new StandaloneGenerationPreviewExpansionHost(this);
        PreviewEntry.ExpansionHost = _previewExpansionHost;
        PreviewEntry.OverflowOwner = PreviewEntry;
    }

    public GenerationMetadataOverlayView(
        IGenerationPreviewSessionFactory previewSessionFactory)
        : this()
    {
        _previewSessionFactory = previewSessionFactory
            ?? throw new ArgumentNullException(nameof(previewSessionFactory));
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
