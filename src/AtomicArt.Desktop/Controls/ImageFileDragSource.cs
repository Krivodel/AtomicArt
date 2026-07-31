using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Controls;

internal static class ImageFileDragSource
{
    private const int DragPreviewWidth = 256;

    internal static DataTransfer CreateDataTransfer(
        IStorageFile file,
        AtomicArtImageDragSourceKind sourceKind)
    {
        ArgumentNullException.ThrowIfNull(file);

        return AtomicArtImageDragData.Create(file, sourceKind);
    }

    internal static async Task StartAsync(
        Control source,
        PointerPressedEventArgs e,
        string imagePath,
        string previewPath,
        AtomicArtImageDragSourceKind sourceKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPath);

        await StartCoreAsync(
            source,
            e,
            imagePath,
            () => CreateOwnedPreviewWindowOrDefault(previewPath),
            sourceKind);
    }

    internal static async Task StartAsync(
        Control source,
        PointerPressedEventArgs e,
        string imagePath,
        Bitmap? previewBitmap,
        AtomicArtImageDragSourceKind sourceKind)
    {
        await StartCoreAsync(
            source,
            e,
            imagePath,
            () => CreateBorrowedPreviewWindowOrDefault(previewBitmap),
            sourceKind);
    }

    private static async Task StartCoreAsync(
        Control source,
        PointerPressedEventArgs e,
        string imagePath,
        Func<ImageDragPreviewWindow?> previewWindowFactory,
        AtomicArtImageDragSourceKind sourceKind)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(e);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(previewWindowFactory);

        TopLevel? topLevel = TopLevel.GetTopLevel(source);

        if (topLevel is null)
        {
            return;
        }

        using IStorageFile? file = await topLevel.StorageProvider
            .TryGetFileFromPathAsync(imagePath);

        if (file is null)
        {
            return;
        }

        DataTransfer dataTransfer = CreateDataTransfer(file, sourceKind);
        using ImageDragPreviewWindow? previewWindow = previewWindowFactory();
        previewWindow?.Start(topLevel as Window);

        await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Copy);
    }

    private static ImageDragPreviewWindow? CreateOwnedPreviewWindowOrDefault(
        string previewPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        Bitmap? bitmap = CreatePreviewBitmapOrDefault(previewPath);

        return bitmap is null
            ? null
            : ImageDragPreviewWindow.CreateOwned(bitmap);
    }

    private static ImageDragPreviewWindow? CreateBorrowedPreviewWindowOrDefault(
        Bitmap? previewBitmap)
    {
        if (!OperatingSystem.IsWindows() || previewBitmap is null)
        {
            return null;
        }

        return ImageDragPreviewWindow.CreateBorrowed(previewBitmap);
    }

    private static Bitmap? CreatePreviewBitmapOrDefault(string previewPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(previewPath);

            return Bitmap.DecodeToWidth(
                stream,
                DragPreviewWidth,
                BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            return null;
        }
    }
}
