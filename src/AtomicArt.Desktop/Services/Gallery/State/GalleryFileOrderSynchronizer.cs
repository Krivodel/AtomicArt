using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryFileOrderSynchronizer
    : IGalleryFileOrderSynchronizer
{
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly GalleryStatePathConverter _pathConverter;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private readonly ILogger<GalleryFileOrderSynchronizer> _logger;

    public GalleryFileOrderSynchronizer(
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GenerationImageFileNamePolicy fileNamePolicy,
        ILogger<GalleryFileOrderSynchronizer> logger)
    {
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _pathConverter = pathConverter
            ?? throw new ArgumentNullException(nameof(pathConverter));
        _fileNamePolicy = fileNamePolicy
            ?? throw new ArgumentNullException(nameof(fileNamePolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SynchronizeAsync(
        IReadOnlyList<GalleryItemState> items,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

        foreach (GalleryItemState item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Status != GenerationItemStatus.Generated
                || item.GalleryOrderTimestampUtc is not DateTime expectedTimestampUtc)
            {
                continue;
            }

            SynchronizeItem(item, expectedTimestampUtc);
        }
    }

    private void SynchronizeItem(
        GalleryItemState item,
        DateTime expectedTimestampUtc)
    {
        string? imagePath = _pathConverter.GetImagePathForDeletion(
            item.ImagePath);

        if (imagePath is null
            || !_fileNamePolicy.IsFileNameForItem(
                Path.GetFileName(imagePath),
                item.Id)
            || !File.Exists(imagePath))
        {
            return;
        }

        try
        {
            if (File.GetLastWriteTimeUtc(imagePath) != expectedTimestampUtc)
            {
                File.SetLastWriteTimeUtc(imagePath, expectedTimestampUtc);
            }

            if (OperatingSystem.IsWindows()
                && File.GetCreationTimeUtc(imagePath) != expectedTimestampUtc)
            {
                File.SetCreationTimeUtc(imagePath, expectedTimestampUtc);
            }
        }
        catch (ArgumentException ex)
        {
            LogSynchronizationFailure(ex, item.Id);
        }
        catch (IOException ex)
        {
            LogSynchronizationFailure(ex, item.Id);
        }
        catch (NotSupportedException ex)
        {
            LogSynchronizationFailure(ex, item.Id);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogSynchronizationFailure(ex, item.Id);
        }
    }

    private void LogSynchronizationFailure(Exception exception, Guid itemId)
    {
        _logger.LogWarning(
            exception,
            "Failed to synchronize file dates for gallery item {ItemId}.",
            itemId);
    }
}
