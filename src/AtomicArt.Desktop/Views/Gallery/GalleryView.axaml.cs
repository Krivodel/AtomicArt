using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GalleryView : UserControl
{
    public IAnimatedGalleryOperations? GalleryOperations { get; }

    public GalleryView()
    {
        InitializeComponent();
    }

    public GalleryView(IAnimatedGalleryOperations galleryOperations)
    {
        ArgumentNullException.ThrowIfNull(galleryOperations);

        GalleryOperations = galleryOperations;
        InitializeComponent();
    }
}
