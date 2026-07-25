using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

public sealed class DragDropImageService : IDragDropImageService
{
    private readonly AttachedImageFileReader _fileReader;
    private readonly ExternalImageAttachmentReader _externalImageReader;
    private readonly IVirtualFileDropInputProvider _virtualFileInputProvider;
    private readonly ILogger<DragDropImageService> _logger;

    public DragDropImageService(
        AttachedImageFileReader fileReader,
        ExternalImageAttachmentReader externalImageReader)
        : this(
            fileReader,
            externalImageReader,
            EmptyVirtualFileDropInputProvider.Instance,
            NullLogger<DragDropImageService>.Instance)
    {
    }

    public DragDropImageService(
        AttachedImageFileReader fileReader,
        ExternalImageAttachmentReader externalImageReader,
        ILogger<DragDropImageService> logger)
        : this(
            fileReader,
            externalImageReader,
            EmptyVirtualFileDropInputProvider.Instance,
            logger)
    {
    }

    internal DragDropImageService(
        AttachedImageFileReader fileReader,
        ExternalImageAttachmentReader externalImageReader,
        IVirtualFileDropInputProvider virtualFileInputProvider,
        ILogger<DragDropImageService> logger)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(externalImageReader);
        ArgumentNullException.ThrowIfNull(virtualFileInputProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _fileReader = fileReader;
        _externalImageReader = externalImageReader;
        _virtualFileInputProvider = virtualFileInputProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<ImageAttachmentInput>> ExtractImagesAsync(
        IDataTransfer dataTransfer,
        int maxInputBytes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ct.ThrowIfCancellationRequested();

        IEnumerable<IStorageItem>? storageItems = dataTransfer.TryGetFiles();

        if (storageItems is not null)
        {
            List<IStorageFile> files = storageItems
                .OfType<IStorageFile>()
                .ToList();

            if (files.Count > 0)
            {
                IReadOnlyList<ImageAttachmentInput> fileInputs = _fileReader.CreateInputs(
                    files,
                    maxInputBytes);
                LogExtractedInputs(fileInputs.Count, "storage files");

                return Task.FromResult(fileInputs);
            }
        }

        if (_virtualFileInputProvider.TryTakeInputs(
                out IReadOnlyList<ImageAttachmentInput> virtualFileInputs))
        {
            LogExtractedInputs(virtualFileInputs.Count, "virtual files");

            return Task.FromResult(virtualFileInputs);
        }

        TransferredImageContent? encodedImage =
            ImageDataTransferContentReader.TryGetEncodedImage(dataTransfer);

        if (encodedImage is not null)
        {
            ImageAttachmentInput encodedInput = ImageAttachmentInputFactory.CreateEncoded(
                "dropped-image",
                encodedImage.Format,
                encodedImage.Content,
                maxInputBytes);
            LogExtractedInputs(1, "encoded image data");

            return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(
                [encodedInput]);
        }

        Bitmap? bitmap = dataTransfer.TryGetBitmap();

        if (bitmap is not null)
        {
            ImageAttachmentInput bitmapInput = ImageAttachmentInputFactory.CreateBitmap(
                "dropped-image.png",
                bitmap,
                maxInputBytes);
            LogExtractedInputs(1, "bitmap data");

            return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(
                [bitmapInput]);
        }

        if (ImageDataTransferUriExtractor.TryGetImageUri(
                dataTransfer,
                out Uri? imageUri)
            && imageUri is not null)
        {
            ImageAttachmentInput externalInput = _externalImageReader.CreateInput(
                imageUri,
                maxInputBytes);
            LogExtractedInputs(1, "external image URI");

            return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(
                [externalInput]);
        }

        _logger.LogDebug(
            "Drag-and-drop data contained no supported image representation.");

        return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(
            Array.Empty<ImageAttachmentInput>());
    }

    private void LogExtractedInputs(int count, string sourceKind)
    {
        _logger.LogInformation(
            "Drag-and-drop {SourceKind} produced {AttachmentCount} image attachment inputs.",
            sourceKind,
            count);
    }

    private sealed class EmptyVirtualFileDropInputProvider
        : IVirtualFileDropInputProvider
    {
        public static EmptyVirtualFileDropInputProvider Instance { get; } = new();

        public bool TryTakeInputs(
            out IReadOnlyList<ImageAttachmentInput> inputs)
        {
            inputs = Array.Empty<ImageAttachmentInput>();
            return false;
        }
    }
}
