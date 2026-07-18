using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Controls.Generation;

internal sealed class AttachmentVisualEntry
{
    public AttachedImageViewModel Item { get; }
    public Control Control { get; }
    public Button RemoveButton { get; }
    public Image Image { get; }
    public AttachmentPixelLoadingControl LoadingIndicator { get; }

    private Bitmap? _bitmap;
    private PropertyChangedEventHandler? _propertyChangedHandler;

    public AttachmentVisualEntry(
        AttachedImageViewModel item,
        Control control,
        Button removeButton,
        Image image,
        AttachmentPixelLoadingControl loadingIndicator)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(removeButton);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(loadingIndicator);

        Item = item;
        Control = control;
        RemoveButton = removeButton;
        Image = image;
        LoadingIndicator = loadingIndicator;
    }

    public void Subscribe(PropertyChangedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        Unsubscribe();
        _propertyChangedHandler = handler;
        Item.PropertyChanged += handler;
    }

    public void Unsubscribe()
    {
        if (_propertyChangedHandler is null)
        {
            return;
        }

        Item.PropertyChanged -= _propertyChangedHandler;
        _propertyChangedHandler = null;
    }

    public void SetBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        Bitmap? previousBitmap = _bitmap;
        _bitmap = bitmap;
        Image.Source = bitmap;
        previousBitmap?.Dispose();
    }

    public void DisposeVisual()
    {
        Unsubscribe();
        Image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
