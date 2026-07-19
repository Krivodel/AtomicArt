using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using AtomicArt.Contracts.Generation;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

public sealed class ClipboardImageService :
    IClipboardImageService,
    IClipboardAttachmentService,
    ITextClipboardService
{
    private const string ClipboardImageFileName = "clipboard.png";
    private const string ClipboardImageTooLargeMessage =
        "Clipboard image exceeds the safe input size limit.";

    private static readonly DataFormat<byte[]>[] PngClipboardFormats =
    [
        DataFormat.CreateBytesPlatformFormat(PicaClipboardFormats.WindowsPng),
        DataFormat.CreateBytesPlatformFormat(PicaClipboardFormats.PngMime),
        DataFormat.CreateBytesPlatformFormat(PicaClipboardFormats.MacOsPng)
    ];

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

        byte[]? pngContent = await TryGetPngContentAsync(dataTransfer)
            .ConfigureAwait(false);

        if (pngContent is not null)
        {
            if (pngContent.LongLength > maxInputBytes)
            {
                _logger.LogWarning(
                    "Clipboard PNG content with {SizeBytes} bytes exceeded the input limit of {MaxInputBytes} bytes.",
                    pngContent.LongLength,
                    maxInputBytes);
                throw new InvalidDataException(
                    ClipboardImageTooLargeMessage);
            }

            _logger.LogInformation(
                "Clipboard PNG content read with {SizeBytes} bytes.",
                pngContent.LongLength);
            return ImageAttachmentInput.FromImage(new AttachedImageDto(
                ClipboardImageFileName,
                PicaImageFormats.PngContentType,
                pngContent));
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
        return new ImageAttachmentInput(
            ClipboardImageFileName,
            read: readCt => EncodeAsync(bitmap, maxInputBytes, readCt),
            ownedResource: bitmap);
    }

    private static async Task<byte[]?> TryGetPngContentAsync(
        IAsyncDataTransfer dataTransfer)
    {
        foreach (DataFormat<byte[]> pngFormat in PngClipboardFormats)
        {
            byte[]? content = await dataTransfer
                .TryGetValueAsync(pngFormat)
                .ConfigureAwait(false);

            if (content is not null)
            {
                return content;
            }
        }

        return null;
    }

    private static async Task<AttachedImageDto?> EncodeAsync(
        Bitmap bitmap,
        int maxInputBytes,
        CancellationToken ct)
    {
        return await Task.Run(
                () => Encode(bitmap, maxInputBytes, ct),
                ct)
            .ConfigureAwait(false);
    }

    private static AttachedImageDto Encode(
        Bitmap bitmap,
        int maxInputBytes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using LimitedMemoryStream stream = new(maxInputBytes);

        try
        {
            bitmap.Save(stream);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException(
                ClipboardImageTooLargeMessage,
                ex);
        }

        ct.ThrowIfCancellationRequested();

        return new AttachedImageDto(
            ClipboardImageFileName,
            PicaImageFormats.PngContentType,
            stream.ToArray());
    }
}
