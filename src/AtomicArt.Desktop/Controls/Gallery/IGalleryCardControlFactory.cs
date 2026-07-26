using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryCardControlFactory
{
    Control Create(
        object item,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost);

    Control CreateTransient(
        object item,
        GalleryCardCommands commands,
        IGenerationPreviewExpansionHost previewExpansionHost);

    void ApplyCommands(Control control, GalleryCardCommands commands);

    bool CanRetainRecycledControl(Control control);

    void Recycle(Control control);
}
