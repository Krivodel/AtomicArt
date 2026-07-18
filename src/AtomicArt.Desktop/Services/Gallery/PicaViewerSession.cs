using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class PicaViewerSession : IViewerActionDispatcher, IAsyncDisposable
{
    public PicaViewerRequest? Request { get; private set; }

    private readonly PicaViewerSessionDependencies _dependencies;
    private readonly HashSet<string> _allowedImagePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _sessionDirectory;
    private IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? _attachImagesCommand;
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

        foreach (GalleryImageViewerItem sourceItem in sourceItems)
        {
            PicaImageItem item = await MaterializeItemAsync(sourceItem, ct).ConfigureAwait(false);
            items.Add(item);
            _allowedImagePaths.Add(Path.GetFullPath(item.FilePath));
        }

        _attachImagesCommand = sourceRequest.AttachImagesCommand;
        List<PicaActionDefinition> actions = [];

        if (_attachImagesCommand is not null)
        {
            actions.Add(AtomicArtPicaActions.Attach);
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

    public async Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        if (!CanDispatch(action))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected unsupported action {ActionId} for item {ItemId}",
                action.Id,
                item.Id);
            return;
        }

        string fullPath = Path.GetFullPath(item.FilePath);

        if (!_allowedImagePaths.Contains(fullPath) || !File.Exists(fullPath))
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

    public async Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(pngContent);

        if (!CanDispatch(action))
        {
            _dependencies.Logger.LogWarning(
                "Embedded Pica rejected unsupported selection action {ActionId} for item {ItemId}",
                action.Id,
                item.Id);
            return;
        }

        await ExecuteAttachAsync(
            PicaImageFormats.SelectionFileName,
            PicaImageFormats.PngContentType,
            pngContent,
            ct).ConfigureAwait(false);
        _dependencies.Logger.LogInformation(
            "Embedded Pica attached selection from item {ItemId} with {ByteCount} bytes",
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
            catch (IOException ex)
            {
                _dependencies.Logger.LogWarning(
                    ex,
                    "Failed to delete embedded Pica temporary files.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _dependencies.Logger.LogWarning(
                    ex,
                    "Access was denied while deleting embedded Pica temporary files.");
            }
        }

        _dependencies.Logger.LogInformation("Embedded Pica viewer session disposed");
        return ValueTask.CompletedTask;
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
            _ => throw new NotSupportedException("The image source cannot be opened in Pica.")
        };
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

    private bool CanDispatch(PicaActionDefinition action)
    {
        return _attachImagesCommand is not null
            && string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal);
    }

    private string GetExtension(string fileName, string contentType)
    {
        if (_dependencies.FormatRegistry.TryGetByContentType(
                contentType,
                out IGenerationImageFormat? contentFormat)
            && contentFormat is not null)
        {
            return contentFormat.Extension;
        }

        if (_dependencies.FormatRegistry.TryGetByFileName(
                fileName,
                out IGenerationImageFormat? fileFormat)
            && fileFormat is not null)
        {
            return fileFormat.Extension;
        }

        return GenerationImageFileFormats.PngExtension;
    }

    private string GetContentType(string fileName)
    {
        if (_dependencies.FormatRegistry.TryGetByFileName(
                fileName,
                out IGenerationImageFormat? format)
            && format is not null)
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
        List<AttachedImageDto> images = [new(safeFileName, contentType, content)];
        await _dependencies.UiThreadDispatcher.InvokeAsync(
            async () =>
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
            },
            ct).ConfigureAwait(false);
    }

    private async void OnWindowClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        await _dependencies.ClipboardImageWriter.FlushAsync(CancellationToken.None);
        await DisposeAsync();
    }
}
