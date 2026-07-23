using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GenerationMetadataOverlayView : UserControl
{
    private readonly StandaloneGenerationPreviewExpansionHost _previewExpansionHost;

    public GenerationMetadataOverlayView()
    {
        InitializeComponent();
        _previewExpansionHost = new StandaloneGenerationPreviewExpansionHost(this);
        PreviewEntry.ExpansionHost = _previewExpansionHost;
        PreviewEntry.OverflowOwner = PreviewEntry;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _previewExpansionHost.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _previewExpansionHost.Detach();
        base.OnDetachedFromVisualTree(e);
    }
}
