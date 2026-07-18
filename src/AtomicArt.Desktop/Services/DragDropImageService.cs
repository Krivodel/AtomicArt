using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

public sealed class DragDropImageService : IDragDropImageService
{
    private readonly AttachedImageFileReader _fileReader;
    private readonly ILogger<DragDropImageService> _logger;

    public DragDropImageService(AttachedImageFileReader fileReader)
        : this(fileReader, NullLogger<DragDropImageService>.Instance)
    {
    }

    public DragDropImageService(
        AttachedImageFileReader fileReader,
        ILogger<DragDropImageService> logger)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(logger);

        _fileReader = fileReader;
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

        if (storageItems is null)
        {
            _logger.LogDebug("Drag-and-drop data contained no storage items.");
            return Task.FromResult<IReadOnlyList<ImageAttachmentInput>>(
                []);
        }

        List<IStorageFile> files = storageItems
            .OfType<IStorageFile>()
            .ToList();

        IReadOnlyList<ImageAttachmentInput> inputs = _fileReader.CreateInputs(
            files,
            maxInputBytes);
        _logger.LogInformation(
            "Drag-and-drop data produced {AttachmentCount} image attachment inputs.",
            inputs.Count);

        return Task.FromResult(inputs);
    }
}
