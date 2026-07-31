using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

internal static class AtomicArtImageDragData
{
    private const string GalleryFormatIdentifier = "AtomicArt.GalleryImage";
    private const string PanelAttachmentFormatIdentifier =
        "AtomicArt.PanelAttachmentImage";

    private static readonly DataFormat<object> GalleryFormat =
        DataFormat.CreateInProcessFormat<object>(GalleryFormatIdentifier);
    private static readonly DataFormat<object> PanelAttachmentFormat =
        DataFormat.CreateInProcessFormat<object>(
            PanelAttachmentFormatIdentifier);
    private static readonly object Marker = new();

    public static DataTransfer Create(
        IStorageFile file,
        AtomicArtImageDragSourceKind sourceKind)
    {
        ArgumentNullException.ThrowIfNull(file);

        DataTransferItem item = DataTransferItem.CreateFile(file);
        item.Set(GetFormat(sourceKind), Marker);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);

        return dataTransfer;
    }

    public static bool IsAtomicArtImage(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return dataTransfer.Contains(GalleryFormat)
            || dataTransfer.Contains(PanelAttachmentFormat);
    }

    public static bool IsGalleryImage(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return dataTransfer.Contains(GalleryFormat);
    }

    private static DataFormat<object> GetFormat(
        AtomicArtImageDragSourceKind sourceKind)
    {
        return sourceKind switch
        {
            AtomicArtImageDragSourceKind.Gallery => GalleryFormat,
            AtomicArtImageDragSourceKind.PanelAttachment =>
                PanelAttachmentFormat,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceKind))
        };
    }
}
