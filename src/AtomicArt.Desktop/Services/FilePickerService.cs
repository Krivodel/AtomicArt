using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Platform.Storage;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class FilePickerService : IFilePickerService, IFilePickerAttachmentService
{
    private readonly AttachedImageFileReader _fileReader;
    private readonly ILogger<FilePickerService> _logger;
    private IStorageProvider? _storageProvider;

    public FilePickerService(AttachedImageFileReader fileReader)
        : this(fileReader, NullLogger<FilePickerService>.Instance)
    {
    }

    public FilePickerService(
        AttachedImageFileReader fileReader,
        ILogger<FilePickerService> logger)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(logger);

        _fileReader = fileReader;
        _logger = logger;
    }

    public void Attach(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        _storageProvider = storageProvider;
    }

    public async Task<IReadOnlyList<ImageAttachmentInput>> PickImagesAsync(
        int maxInputBytes,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ct.ThrowIfCancellationRequested();

        if (_storageProvider is null || !_storageProvider.CanOpen)
        {
            _logger.LogWarning("Image file picker is unavailable.");
            return [];
        }

        FilePickerOpenOptions options = new()
        {
            AllowMultiple = true,
            FileTypeFilter =
            [
                FilePickerFileTypes.ImageAll
            ],
            Title = UiStrings.PickImagesTitle
        };
        IReadOnlyList<IStorageFile> files = await _storageProvider
            .OpenFilePickerAsync(options)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Image file picker returned {SelectedFileCount} files.",
            files.Count);

        return _fileReader.CreateInputs(files, maxInputBytes);
    }
}
