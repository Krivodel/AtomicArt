using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

public sealed class ClipboardImageService :
    IClipboardImageService,
    IClipboardAttachmentService,
    ITextClipboardService
{
    private const string ClipboardImageFileName = "clipboard.png";

    private readonly AttachedImageFileReader _fileReader;
    private readonly ILogger<ClipboardImageService> _logger;
    private IClipboard? _clipboard;

    public ClipboardImageService(AttachedImageFileReader fileReader)
        : this(fileReader, NullLogger<ClipboardImageService>.Instance)
    {
    }

    public ClipboardImageService(
        AttachedImageFileReader fileReader,
        ILogger<ClipboardImageService> logger)
    {
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Attach(IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        _clipboard = clipboard;
    }

    public async Task SetTextAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();

        IClipboard clipboard = _clipboard
            ?? throw new InvalidOperationException("Clipboard is not attached.");

        await clipboard.SetTextAsync(text).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    public async Task<ImageAttachmentInput?> TryGetImageAsync(
        int maxInputBytes,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ct.ThrowIfCancellationRequested();

        if (_clipboard is null)
        {
            _logger.LogDebug("Clipboard image read skipped because clipboard is not attached.");
            return null;
        }

        using IAsyncDataTransfer? dataTransfer = await _clipboard
            .TryGetDataAsync()
            .ConfigureAwait(false);

        if (dataTransfer is null)
        {
            _logger.LogDebug("Clipboard contained no transferable data.");
            return null;
        }

        IReadOnlyList<IStorageItem>? storageItems = await dataTransfer
            .TryGetFilesAsync()
            .ConfigureAwait(false);
        IStorageFile? file = storageItems?
            .OfType<IStorageFile>()
            .FirstOrDefault();

        if (file is not null)
        {
            _logger.LogInformation("Clipboard image will be read from a storage item.");
            return _fileReader.CreateInput(file, maxInputBytes);
        }

        TransferredImageContent? encodedImage = await ImageDataTransferContentReader
            .TryGetEncodedImageAsync(dataTransfer)
            .ConfigureAwait(false);

        if (encodedImage is not null)
        {
            _logger.LogInformation(
                "Clipboard encoded image content read with {SizeBytes} bytes and content type {ContentType}.",
                encodedImage.Content.LongLength,
                encodedImage.Format.ContentType);

            return ImageAttachmentInputFactory.CreateEncoded(
                Path.GetFileNameWithoutExtension(ClipboardImageFileName),
                encodedImage.Format,
                encodedImage.Content,
                maxInputBytes);
        }

        Bitmap? bitmap = await dataTransfer
            .TryGetBitmapAsync()
            .ConfigureAwait(false);

        if (bitmap is null)
        {
            _logger.LogDebug("Clipboard contained no supported image representation.");
            return null;
        }

        _logger.LogInformation("Clipboard bitmap image will be encoded on demand.");
        return ImageAttachmentInputFactory.CreateBitmap(
            ClipboardImageFileName,
            bitmap,
            maxInputBytes);
    }
}
