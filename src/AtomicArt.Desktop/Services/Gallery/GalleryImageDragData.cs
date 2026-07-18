using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services.Gallery;

internal static class GalleryImageDragData
{
    private const string FormatIdentifier = "AtomicArt.GalleryImage";

    private static readonly DataFormat<object> Format =
        DataFormat.CreateInProcessFormat<object>(FormatIdentifier);
    private static readonly object Marker = new();

    public static DataTransfer Create(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        DataTransferItem item = DataTransferItem.CreateFile(file);
        item.Set(Format, Marker);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);

        return dataTransfer;
    }

    public static bool IsGalleryImage(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return dataTransfer.Contains(Format);
    }
}
