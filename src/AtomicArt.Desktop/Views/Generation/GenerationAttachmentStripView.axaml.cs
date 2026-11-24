using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationAttachmentStripView : UserControl
{
    public double ActionsProgress
    {
        get => GetValue(ActionsProgressProperty);
        set => SetValue(ActionsProgressProperty, value);
    }
    public bool KeepActionsVisible
    {
        get => GetValue(KeepActionsVisibleProperty);
        set => SetValue(KeepActionsVisibleProperty, value);
    }
    public GenerationReferencePreviewLoader? PreviewLoader
    {
        get => GetValue(PreviewLoaderProperty);
        set => SetValue(PreviewLoaderProperty, value);
    }

    public static readonly StyledProperty<GenerationReferencePreviewLoader?> PreviewLoaderProperty =
        AvaloniaProperty.Register<GenerationAttachmentStripView, GenerationReferencePreviewLoader?>(
            nameof(PreviewLoader));
    public static readonly StyledProperty<bool> KeepActionsVisibleProperty =
        AvaloniaProperty.Register<GenerationAttachmentStripView, bool>(nameof(KeepActionsVisible));
    public static readonly StyledProperty<double> ActionsProgressProperty =
        AvaloniaProperty.Register<GenerationAttachmentStripView, double>(nameof(ActionsProgress), defaultValue: 1d);

    public GenerationAttachmentStripView()
    {
        InitializeComponent();
    }
}
