using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using Pica.Protocol;

namespace Pica.Viewer.Services;

internal sealed class ViewerImageOperations
{
    private readonly IViewerClipboardWriter _clipboardImageWriter;
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly PngImageEncoder _pngImageEncoder;
    private readonly IViewerActionDispatcher _actionDispatcher;

    internal ViewerImageOperations(
        IViewerClipboardWriter clipboardImageWriter,
        IImageFormatRegistry formatRegistry,
        PngImageEncoder pngImageEncoder,
        IViewerActionDispatcher actionDispatcher)
    {
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _pngImageEncoder = pngImageEncoder
            ?? throw new ArgumentNullException(nameof(pngImageEncoder));
        _actionDispatcher = actionDispatcher
            ?? throw new ArgumentNullException(nameof(actionDispatcher));
    }

    internal void AttachClipboard(IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        _clipboardImageWriter.Attach(clipboard);
    }

    internal async Task CopyPreparedImageAsync(
        PreparedClipboardImage image,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);

        await _clipboardImageWriter.SetPreparedImageAsync(image, ct);
    }

    internal async Task FlushClipboardAsync(CancellationToken ct)
    {
        await _clipboardImageWriter.FlushAsync(ct);
    }

    internal async Task CopyFileAsync(
        IStorageProvider storageProvider,
        PicaImageItem item,
        Bitmap? bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(item);

        IStorageFile? file = await storageProvider.TryGetFileFromPathAsync(item.FilePath);

        if (file is null)
        {
            return;
        }

        if (bitmap is null)
        {
            await _clipboardImageWriter.SetFileAsync(file, ct);
            return;
        }

        await _clipboardImageWriter.SetFileWithImageAsync(file, bitmap, ct);
    }

    internal async Task DispatchCurrentAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        await _actionDispatcher.DispatchCurrentImageAsync(action, item, ct);
    }

    internal async Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        Bitmap bitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);

        byte[] pngContent = await _pngImageEncoder.EncodeAsync(bitmap, ct);
        await _actionDispatcher.DispatchSelectionAsync(action, item, pngContent, ct);
    }

    internal async Task SaveCurrentAsync(
        IStorageProvider storageProvider,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(item);

        if (!storageProvider.CanSave)
        {
            return;
        }

        string fileName = item.FileName;
        string extension = Path.GetExtension(fileName);
        IStorageFile? destination = await ShowSaveFilePickerAsync(
            storageProvider,
            fileName,
            CreateImageFilePickerFileType(extension, fileName));

        if (destination is null)
        {
            return;
        }

        byte[] content = await File.ReadAllBytesAsync(item.FilePath, ct);
        await using Stream target = await destination.OpenWriteAsync();
        ClearWritableStream(target);
        await target.WriteAsync(content, ct);
    }

    internal async Task SaveSelectionAsync(
        IStorageProvider storageProvider,
        Bitmap bitmap,
        Action saved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(saved);

        if (!storageProvider.CanSave)
        {
            return;
        }

        IStorageFile? destination = await ShowSaveFilePickerAsync(
            storageProvider,
            PicaImageFormats.SelectionFileName,
            new FilePickerFileType("PNG")
            {
                MimeTypes = [PicaImageFormats.PngContentType],
                Patterns = ["*" + PicaImageFormats.PngExtension]
            });

        if (destination is null)
        {
            return;
        }

        await using Stream target = await destination.OpenWriteAsync();
        ClearWritableStream(target);
        byte[] pngContent = await _pngImageEncoder.EncodeAsync(bitmap, ct);
        await target.WriteAsync(pngContent, ct);
        saved();
    }

    private async Task<IStorageFile?> ShowSaveFilePickerAsync(
        IStorageProvider storageProvider,
        string suggestedFileName,
        FilePickerFileType fileType)
    {
        FilePickerSaveOptions options = new()
        {
            FileTypeChoices = [fileType],
            SuggestedFileName = suggestedFileName,
            Title = "Сохранить как"
        };

        return await storageProvider.SaveFilePickerAsync(options);
    }

    private FilePickerFileType CreateImageFilePickerFileType(
        string extension,
        string fallbackFileName)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = _formatRegistry.GetExtension(fallbackFileName);
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = PicaImageFormats.PngExtension;
        }

        string normalizedExtension = extension.StartsWith('.')
            ? extension
            : "." + extension;
        string label = normalizedExtension.TrimStart('.').ToUpperInvariant();

        return new FilePickerFileType(label)
        {
            Patterns = ["*" + normalizedExtension]
        };
    }

    private static void ClearWritableStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.SetLength(0);
        }
    }
}
