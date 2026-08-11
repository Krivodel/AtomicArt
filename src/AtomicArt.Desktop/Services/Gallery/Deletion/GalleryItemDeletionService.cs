using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery.Deletion;

public sealed class GalleryItemDeletionService : IGalleryItemDeletionService
{
    private const string ImageFileKind = "image";
    private const string ThumbnailFileKind = "thumbnail";

    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly ILogger<GalleryItemDeletionService> _logger;

    public GalleryItemDeletionService(
        ITrustedImageFileService trustedImageFileService,
        GenerationImageFileNamePolicy fileNamePolicy,
        IDataRootAccessCoordinator accessCoordinator,
        ILogger<GalleryItemDeletionService> logger)
    {
        ArgumentNullException.ThrowIfNull(trustedImageFileService);
        ArgumentNullException.ThrowIfNull(fileNamePolicy);
        ArgumentNullException.ThrowIfNull(accessCoordinator);
        ArgumentNullException.ThrowIfNull(logger);

        _trustedImageFileService = trustedImageFileService;
        _fileNamePolicy = fileNamePolicy;
        _accessCoordinator = accessCoordinator;
        _logger = logger;
    }

    public async Task DeleteFilesAsync(
        GalleryItemDeletionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        GalleryItemDeletionRequest[] requests = [request];
        await DeleteFilesAsync(requests, ct).ConfigureAwait(false);
    }

    public async Task DeleteFilesAsync(
        IReadOnlyList<GalleryItemDeletionRequest> requests,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requests);

        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        foreach (GalleryItemDeletionRequest request in requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            DeleteFiles(request, ct);
        }
    }

    private void DeleteFiles(
        GalleryItemDeletionRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Deleting managed gallery files for item {ItemId}",
            request.ItemId);

        DeleteFileIfTrusted(request, request.ImagePath, ImageFileKind, ct);
        DeleteFileIfTrusted(request, request.ThumbnailPath, ThumbnailFileKind, ct);
        _logger.LogInformation(
            "Completed managed gallery file deletion for item {ItemId}",
            request.ItemId);
    }

    private void DeleteFileIfTrusted(
        GalleryItemDeletionRequest request,
        string? path,
        string fileKind,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!IsManagedFileForItem(path, request.ItemId))
        {
            _logger.LogWarning(
                "Gallery item {ItemId} {FileKind} file path does not belong to the item and was not deleted.",
                request.ItemId,
                fileKind);

            return;
        }

        try
        {
            _trustedImageFileService.DeleteTrustedImageFileIfExists(
                path,
                request.ModelId,
                resolvedPath => EnsureManagedFileForItem(resolvedPath, request.ItemId));
            _logger.LogDebug(
                "Processed gallery item {ItemId} {FileKind} file deletion",
                request.ItemId,
                fileKind);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Gallery item {ItemId} {FileKind} file path is not trusted and was not deleted.",
                request.ItemId,
                fileKind);
        }
        catch (ArgumentException ex)
        {
            LogDeletionFailure(ex, request.ItemId, fileKind);
        }
        catch (IOException ex)
        {
            LogDeletionFailure(ex, request.ItemId, fileKind);
        }
        catch (NotSupportedException ex)
        {
            LogDeletionFailure(ex, request.ItemId, fileKind);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeletionFailure(ex, request.ItemId, fileKind);
        }
    }

    private bool IsManagedFileForItem(string path, Guid itemId)
    {
        string fileName = Path.GetFileName(path);

        return _fileNamePolicy.IsFileNameForItem(fileName, itemId);
    }

    private void EnsureManagedFileForItem(string path, Guid itemId)
    {
        if (IsManagedFileForItem(path, itemId))
        {
            return;
        }

        throw new InvalidOperationException("Resolved gallery item file path does not belong to the item.");
    }

    private void LogDeletionFailure(
        Exception exception,
        Guid itemId,
        string fileKind)
    {
        _logger.LogError(
            exception,
            "Failed to delete gallery item {ItemId} {FileKind} file.",
            itemId,
            fileKind);
    }
}
