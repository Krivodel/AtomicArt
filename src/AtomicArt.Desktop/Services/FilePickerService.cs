using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Platform.Storage;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services;

public sealed class FilePickerService :
    IFilePickerService,
    IFolderPickerService,
    IFilePickerAttachmentService
{
    private readonly AttachedImageFileReader _fileReader;
    private readonly ILogger<FilePickerService> _logger;
    private readonly ILocalizationTextProvider _textProvider;
    private IStorageProvider? _storageProvider;

    public FilePickerService(
        AttachedImageFileReader fileReader,
        ILocalizationTextProvider textProvider)
        : this(
            fileReader,
            NullLogger<FilePickerService>.Instance,
            textProvider)
    {
    }

    public FilePickerService(
        AttachedImageFileReader fileReader,
        ILogger<FilePickerService> logger,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(textProvider);

        _fileReader = fileReader;
        _logger = logger;
        _textProvider = textProvider;
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
                AttachmentFilePickerFileTypes.Images
            ],
            Title = _textProvider.Get(
                GenerationUiLocalizationKeys.Actions.PickImagesTitle)
        };
        IReadOnlyList<IStorageFile> files = await _storageProvider
            .OpenFilePickerAsync(options)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Image file picker returned {SelectedFileCount} files.",
            files.Count);

        return _fileReader.CreateInputs(files, maxInputBytes);
    }

    public async Task<string?> PickFolderAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_storageProvider is null || !_storageProvider.CanPickFolder)
        {
            _logger.LogWarning("Folder picker is unavailable.");
            return null;
        }

        FolderPickerOpenOptions options = new()
        {
            AllowMultiple = false,
            Title = _textProvider.Get(SettingsLocalizationKeys.DataRoot.PickerTitle)
        };
        IReadOnlyList<IStorageFolder> folders = await _storageProvider
            .OpenFolderPickerAsync(options)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        string? selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        _logger.LogInformation(
            "Folder picker returned a local data directory: {HasLocalDirectory}.",
            !string.IsNullOrWhiteSpace(selectedPath));

        return selectedPath;
    }
}
