using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

internal static class AtomicArtImageDragData
{
    private const string GalleryFormatIdentifier = "AtomicArt.GalleryImage";
    private const string PanelAttachmentFormatIdentifier =
        "AtomicArt.PanelAttachmentImage";
    private const string Dlss5ResultFormatIdentifier = "AtomicArt.Dlss5ResultImage";

    private static readonly DataFormat<object> GalleryFormat =
        DataFormat.CreateInProcessFormat<object>(GalleryFormatIdentifier);
    private static readonly DataFormat<object> PanelAttachmentFormat =
        DataFormat.CreateInProcessFormat<object>(
            PanelAttachmentFormatIdentifier);
    private static readonly DataFormat<object> Dlss5ResultFormat =
        DataFormat.CreateInProcessFormat<object>(Dlss5ResultFormatIdentifier);
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

    public static DataTransfer CreateDlss5Result(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        DataTransferItem item = DataTransferItem.Create(Dlss5ResultFormat, (object)image);
        item.SetBitmap(image);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);

        return dataTransfer;
    }

    public static bool IsAtomicArtImage(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return dataTransfer.Contains(GalleryFormat)
            || dataTransfer.Contains(PanelAttachmentFormat)
            || dataTransfer.Contains(Dlss5ResultFormat);
    }

    public static object? GetDlss5ResultOrDefault(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        foreach (IDataTransferItem item in dataTransfer.Items)
        {
            if (item.TryGetRaw(Dlss5ResultFormat) is object image)
            {
                return image;
            }
        }

        return null;
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
