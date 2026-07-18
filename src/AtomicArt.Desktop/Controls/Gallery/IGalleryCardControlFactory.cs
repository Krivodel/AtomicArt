using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryCardControlFactory
{
    Control Create(object item, GalleryCardCommands commands);

    void ApplyCommands(Control control, GalleryCardCommands commands);
}
