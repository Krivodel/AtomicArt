using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class PicaViewerSession : IViewerActionDispatcher, IAsyncDisposable
{
    public PicaViewerRequest? Request { get; private set; }

    internal IReadOnlyDictionary<Guid, IPicaImageBitmapSource> BitmapSources =>
        new Dictionary<Guid, IPicaImageBitmapSource>(_bitmapSources);

    internal event EventHandler? Disposed;

    private const int StreamCopyBufferSize = 128 * 1024;

    private readonly PicaViewerSessionDependencies _dependencies;
    private readonly HashSet<string> _allowedImagePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _temporaryImagePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, IPicaImageBitmapSource> _bitmapSources = [];
    private readonly Dictionary<Guid, Func<CancellationToken, Task>> _toggleFavoriteActions = [];
    private readonly HashSet<Guid> _galleryItemIds = [];
    private readonly string _sessionDirectory;
    private IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? _attachImagesCommand;
    private IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? _attachImageInputsCommand;
    private ImageViewerWindow? _window;
    private bool _isDisposed;

    public PicaViewerSession(PicaViewerSessionDependencies dependencies)
    {
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _sessionDirectory = Path.Combine(
            Path.GetTempPath(),
            AtomicArtPathNames.RootDirectory,
            PicaProtocolConstants.ApplicationName,
            Guid.NewGuid().ToString("N"));
    }

    public async Task PrepareAsync(GalleryImageViewerRequest sourceRequest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceRequest);
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<GalleryImageViewerItem> sourceItems = sourceRequest.ItemsSource.GetItems();
        List<PicaImageItem> items = [];
        _bitmapSources.Clear();
        _toggleFavoriteActions.Clear();
        bool canShowInGallery = (sourceItems.Count > 0)
            && (sourceItems.All(item => item.Source is GalleryFileImageViewerSource
            { DeleteImageWhenClosed: false }));
        bool canToggleFavorite = (canShowInGallery)
            && (sourceItems.All(item => item.ToggleFavoriteAsync is not null));

        foreach (GalleryImageViewerItem sourceItem in sourceItems)
        {
            PicaImageItem item = await MaterializeItemAsync(sourceItem, ct).ConfigureAwait(false);
            items.Add(item);
            _allowedImagePaths.Add(Path.GetFullPath(item.FilePath));

            if (sourceItem.Source is GalleryBitmapImageViewerSource bitmapSource)
            {
                _bitmapSources[sourceItem.Id] = bitmapSource.BitmapSource;
            }

            if (sourceItem.Source is GalleryFileImageViewerSource fileSource)
            {
                _galleryItemIds.Add(sourceItem.Id);
                if (fileSource.DeleteImageWhenClosed)
                {
                    _temporaryImagePaths.Add(Path.GetFullPath(item.FilePath));
                }
            }

            if ((sourceItem.Source is GalleryFileImageViewerSource
                { DeleteImageWhenClosed: false })
                && (sourceItem.ToggleFavoriteAsync is not null))
            {
                _toggleFavoriteActions[sourceItem.Id] = sourceItem.ToggleFavoriteAsync;
            }
        }

        _attachImagesCommand = sourceRequest.AttachImagesCommand;
        _attachImageInputsCommand = sourceRequest.AttachImageInputsCommand;
        List<PicaActionDefinition> actions = [];

        if (_attachImagesCommand is not null)
        {
            actions.Add(_dependencies.Actions.Attach);
        }

        if (canToggleFavorite)
        {
            actions.Add(_dependencies.Actions.Imba);
        }

        actions.Add(_dependencies.Actions.OpenDlss5);

        if (canShowInGallery)
        {
            actions.Add(_dependencies.Actions.ShowInGallery);
        }

        Request = new PicaViewerRequest(
            items,
            sourceRequest.SelectedItemId,
            actions,
            null);
        _dependencies.Logger.LogDebug(
            "Prepared embedded Pica session with {ItemCount} images and {ActionCount} actions",
            items.Count,
            actions.Count);
    }

    public void AttachWindow(ImageViewerWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_window is not null)
        {
            throw new InvalidOperationException("The Pica viewer session already owns a window.");
        }

        _window = window;
        _window.Closed += OnWindowClosed;
    }

    public bool CanDispatchBitmapWithoutEncoding(
        PicaActionDefinition action,
        PicaImageItem item)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        return CanDispatchAttach(action)
            && _attachImageInputsCommand is not null;
    }

    public async Task DispatchBitmapAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        Bitmap bitmap,
        string fileName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? command =
            _attachImageInputsCommand;

        if (!CanDispatchAttach(action) || command is null)
        {
            throw new InvalidOperationException(
                "The direct bitmap attachment command is unavailable for this Pica session.");
        }

        string safeFileName = Path.GetFileName(fileName);
        using ImageAttachmentInput input = ImageAttachmentInput.FromBorrowedBitmap(
            safeFileName,
            bitmap);
        List<ImageAttachmentInput> inputs = [input];
        await _dependencies.UiThreadDispatcher.InvokeAsync(
            () => ExecuteAttachmentCommandAsync(command, inputs),
            ct).ConfigureAwait(false);
    }

    public async Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        if (string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal))
        {
            await DispatchToggleFavoriteAsync(item, ct).ConfigureAwait(false);
            return;
        }

        if (string.Equals(
                action.Id,
                AtomicArtPicaActions.ShowInGalleryId,
                StringComparison.Ordinal))
        {
            await DispatchShowInGalleryAsync(item, ct).ConfigureAwait(false);
            return;
        }

        if (string.Equals(action.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal))
        {
            await DispatchOpenDlss5Async(item, ct).ConfigureAwait(false);
            return;
        }

        if (!CanDispatchAttach(action))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected unsupported action {ActionId} for item {ItemId}",
                action.Id,
                item.Id);
            return;
        }

        string fullPath = Path.GetFullPath(item.FilePath);

        if ((!_allowedImagePaths.Contains(fullPath)) || (!File.Exists(fullPath)))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected an unavailable current-image action for item {ItemId}",
                item.Id);
            return;
        }

        byte[] content = await File.ReadAllBytesAsync(fullPath, ct).ConfigureAwait(false);
        await ExecuteAttachAsync(
            item.FileName,
            GetContentType(item.FileName),
            content,
            ct).ConfigureAwait(false);
        _dependencies.Logger.LogInformation(
            "Embedded Pica attached current image {ItemId} with {ByteCount} bytes",
            item.Id,
            content.Length);
    }

    public Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct)
    {
        return DispatchDerivedImageAsync(
            action,
            item,
            PicaImageFormats.SelectionFileName,
            pngContent,
            ct);
    }

    public async Task DispatchDerivedImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        string fileName,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(pngContent);

        if (string.Equals(action.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal))
        {
            using MemoryStream source = new(pngContent, writable: false);
            await DispatchOpenDlss5Async(source.CopyToAsync, PicaImageFormats.PngExtension, ct).ConfigureAwait(false);
            return;
        }

        if (!CanDispatchAttach(action))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected unsupported derived-image action {ActionId} for item {ItemId}",
                action.Id,
                item.Id);
            return;
        }

        string safeFileName = Path.GetFileName(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeFileName);
        await ExecuteAttachAsync(
            safeFileName,
            PicaImageFormats.PngContentType,
            pngContent,
            ct).ConfigureAwait(false);
        _dependencies.Logger.LogInformation(
            "Embedded Pica attached derived image from item {ItemId} with {ByteCount} bytes",
            item.Id,
            pngContent.Length);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _isDisposed = true;
        _dependencies.Logger.LogDebug("Disposing embedded Pica viewer session");

        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }

        if (Directory.Exists(_sessionDirectory))
        {
            try
            {
                Directory.Delete(_sessionDirectory, true);
            }
            catch (IOException exception)
            {
                _dependencies.Logger.LogWarning(
                    exception,
                    "Failed to delete embedded Pica temporary files.");
            }
            catch (UnauthorizedAccessException exception)
            {
                _dependencies.Logger.LogWarning(
                    exception,
                    "Access was denied while deleting embedded Pica temporary files.");
            }
        }

        foreach (string temporaryImagePath in _temporaryImagePaths)
        {
            try
            {
                PicaViewerSession.DeleteFileIfExists(temporaryImagePath);
            }
            catch (IOException exception)
            {
                _dependencies.Logger.LogWarning(
                    exception,
                    "Failed to delete temporary Pica image {ImagePath}.",
                    temporaryImagePath);
            }
            catch (UnauthorizedAccessException exception)
            {
                _dependencies.Logger.LogWarning(
                    exception,
                    "Access was denied while deleting temporary Pica image {ImagePath}.",
                    temporaryImagePath);
            }
        }
        _temporaryImagePaths.Clear();
        _bitmapSources.Clear();
        _toggleFavoriteActions.Clear();

        _dependencies.Logger.LogInformation("Embedded Pica viewer session disposed");
        Disposed?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }

    internal async Task CloseAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window.Close();
            _window = null;
        }

        await _dependencies.ClipboardImageWriter.FlushAsync(ct).ConfigureAwait(false);
        await DisposeAsync();
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<PicaImageItem> MaterializeItemAsync(
        GalleryImageViewerItem item,
        CancellationToken ct)
    {
        return item.Source switch
        {
            GalleryFileImageViewerSource fileSource => CreateFileItem(item.Id, fileSource),
            GalleryAttachedImageViewerSource attachedSource =>
                await CreateAttachedItemAsync(item.Id, attachedSource.Image, ct).ConfigureAwait(false),
            GalleryBitmapImageViewerSource bitmapSource =>
                CreateBitmapItem(item.Id, bitmapSource),
            _ => throw new NotSupportedException("The image source cannot be opened in Pica.")
        };
    }

    private PicaImageItem CreateBitmapItem(
        Guid itemId,
        GalleryBitmapImageViewerSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string filePath;

        if (source.FilePath is null)
        {
            filePath = Path.Combine(
                Path.GetTempPath(),
                AtomicArtPathNames.RootDirectory,
                PicaProtocolConstants.ApplicationName,
                "virtual",
                itemId.ToString("N") + Path.GetExtension(source.FileName));
        }
        else
        {
            filePath = _dependencies.TrustedImageFileService.GetTrustedImagePath(
                source.FilePath,
                source.ModelId);
        }

        return new PicaImageItem(
            itemId,
            filePath,
            Path.GetFileName(source.FileName));
    }

    private PicaImageItem CreateFileItem(Guid itemId, GalleryFileImageViewerSource source)
    {
        string trustedPath = _dependencies.TrustedImageFileService.GetTrustedImagePath(
            source.ImagePath,
            source.ModelId);
        string? trustedThumbnailPath =
            _dependencies.TrustedImageFileService.GetTrustedImagePathOrDefault(
                source.ThumbnailPath,
                source.ModelId);

        return new PicaImageItem(
            itemId,
            trustedPath,
            Path.GetFileName(trustedPath),
            trustedThumbnailPath);
    }

    private async Task<PicaImageItem> CreateAttachedItemAsync(
        Guid itemId,
        AttachedImageDto image,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_sessionDirectory);
        string extension = GetExtension(image.FileName, image.ContentType);
        string filePath = Path.Combine(_sessionDirectory, itemId.ToString("N") + extension);
        await File.WriteAllBytesAsync(filePath, image.Content, ct).ConfigureAwait(false);

        return new PicaImageItem(itemId, filePath, Path.GetFileName(image.FileName));
    }

    private bool CanDispatchAttach(PicaActionDefinition action)
    {
        return (_attachImagesCommand is not null)
            && (string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));
    }

    private async Task DispatchShowInGalleryAsync(
        PicaImageItem item,
        CancellationToken ct)
    {
        if (!_galleryItemIds.Contains(item.Id))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected show-in-gallery action for unavailable item {ItemId}",
                item.Id);
            return;
        }

        await _dependencies.UiThreadDispatcher.InvokeAsync(
            async () =>
            {
                try
                {
                    _dependencies.WindowStateService.ShowAndActivate();
                    await _dependencies.GalleryOperations
                        .RevealAsync(item.Id, ct);
                }
                finally
                {
                    _window?.Close();
                }
            },
            ct).ConfigureAwait(false);
        _dependencies.Logger.LogInformation(
            "Embedded Pica revealed gallery item {ItemId}",
            item.Id);
    }

    private async Task DispatchToggleFavoriteAsync(PicaImageItem item, CancellationToken ct)
    {
        if (!_toggleFavoriteActions.TryGetValue(
                item.Id,
                out Func<CancellationToken, Task>? toggleFavoriteAsync))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected IMBA action for unavailable gallery item {ItemId}",
                item.Id);
            return;
        }

        await _dependencies.UiThreadDispatcher.InvokeAsync(
            () => toggleFavoriteAsync(ct),
            ct).ConfigureAwait(false);
        _dependencies.Logger.LogInformation(
            "Embedded Pica toggled favorite state for gallery item {ItemId}",
            item.Id);
    }

    private async Task DispatchOpenDlss5Async(PicaImageItem item, CancellationToken ct)
    {
        string fullPath = Path.GetFullPath(item.FilePath);

        if ((!_allowedImagePaths.Contains(fullPath)) || (!File.Exists(fullPath)))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected DLSS 5 action for unavailable image {ItemId}",
                item.Id);
            return;
        }

        await DispatchOpenDlss5Async(
            async (destination, copyCt) =>
            {
                await using FileStream source = new(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: StreamCopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, copyCt).ConfigureAwait(false);
            },
            Path.GetExtension(fullPath),
            ct).ConfigureAwait(false);
    }

    private async Task DispatchOpenDlss5Async(
        Func<Stream, CancellationToken, Task> writeSourceAsync,
        string extension,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            AtomicArtPathNames.RootDirectory,
            "dlss5");
        Directory.CreateDirectory(stagingDirectory);
        string stagingPath = Path.Combine(
            stagingDirectory,
            Guid.NewGuid().ToString("N") + extension);

        try
        {
            await using (FileStream destination = new(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: StreamCopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await writeSourceAsync(destination, ct).ConfigureAwait(false);
            }

            await _dependencies.UiThreadDispatcher.InvokeAsync(
                async () =>
                {
                    _dependencies.WindowStateService.ShowAndActivate();
                    await _dependencies.Dlss5SourceOpener.OpenFromImagePathAsync(stagingPath, ct);
                    _window?.Close();
                },
                ct).ConfigureAwait(false);
        }
        finally
        {
            PicaViewerSession.DeleteFileIfExists(stagingPath);
        }
    }

    private string GetExtension(string fileName, string contentType)
    {
        if ((_dependencies.FormatRegistry.TryGetByContentType(
                contentType,
                out IGenerationImageFormat? contentFormat))
            && (contentFormat is not null))
        {
            return contentFormat.Extension;
        }

        if ((_dependencies.FormatRegistry.TryGetByFileName(
                fileName,
                out IGenerationImageFormat? fileFormat))
            && (fileFormat is not null))
        {
            return fileFormat.Extension;
        }

        return GenerationImageFileFormats.PngExtension;
    }

    private string GetContentType(string fileName)
    {
        if ((_dependencies.FormatRegistry.TryGetByFileName(
                fileName,
                out IGenerationImageFormat? format))
            && (format is not null))
        {
            return format.ContentType;
        }

        return GenerationImageContentTypes.Png;
    }

    private async Task ExecuteAttachAsync(
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct)
    {
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? command = _attachImagesCommand;

        if (command is null)
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica attach action is unavailable for this session");
            return;
        }

        string safeFileName = Path.GetFileName(fileName);
        List<AttachedImageDto> images = [new AttachedImageDto(safeFileName, contentType, content)];
        await _dependencies.UiThreadDispatcher.InvokeAsync(
            () => ExecuteAttachmentCommandAsync(command, images),
            ct).ConfigureAwait(false);
    }

    private async Task ExecuteAttachmentCommandAsync<TImage>(
        IAsyncRelayCommand<IReadOnlyList<TImage>?> command,
        IReadOnlyList<TImage> images)
    {
        if (command.CanExecute(images))
        {
            await command.ExecuteAsync(images);
            _dependencies.Logger.LogDebug(
                "Embedded Pica delivered {ImageCount} attachment to the generation panel",
                images.Count);
        }
        else
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica attachment was rejected by the generation panel");
        }
    }

    private async void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        await _dependencies.ClipboardImageWriter.FlushAsync(CancellationToken.None);
        await DisposeAsync();
    }
}
