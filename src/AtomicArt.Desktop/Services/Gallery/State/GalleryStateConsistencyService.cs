using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateConsistencyService : IGalleryStateConsistencyService
{
    private readonly IAppStateStore _stateStore;
    private readonly IGalleryItemDeletionService _deletionService;
    private readonly IGalleryFileOrderSynchronizer _fileOrderSynchronizer;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly GalleryStatePathConverter _pathConverter;
    private readonly GalleryStateSection _section;
    private readonly GalleryOrderTimestampNormalizer _timestampNormalizer;
    private readonly ILogger<GalleryStateConsistencyService> _logger;

    public GalleryStateConsistencyService(
        IAppStateStore stateStore,
        IGalleryItemDeletionService deletionService,
        IGalleryFileOrderSynchronizer fileOrderSynchronizer,
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GalleryStateSection section,
        GalleryOrderTimestampNormalizer timestampNormalizer,
        ILogger<GalleryStateConsistencyService> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deletionService = deletionService
            ?? throw new ArgumentNullException(nameof(deletionService));
        _fileOrderSynchronizer = fileOrderSynchronizer
            ?? throw new ArgumentNullException(nameof(fileOrderSynchronizer));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _pathConverter = pathConverter ?? throw new ArgumentNullException(nameof(pathConverter));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _timestampNormalizer = timestampNormalizer
            ?? throw new ArgumentNullException(nameof(timestampNormalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
        GalleryState state = await _stateStore
            .LoadAsync<GalleryState>(_section, ct)
            .ConfigureAwait(false);
        List<GalleryItemState> missingImageItems = state.Items
            .Where(HasMissingGeneratedImage)
            .ToList();

        foreach (GalleryItemState item in missingImageItems)
        {
            GalleryItemDeletionRequest request = new(
                item.Id,
                item.ModelId,
                _pathConverter.GetImagePathForDeletion(item.ImagePath),
                _pathConverter.GetThumbnailPathForDeletion(item.ThumbnailPath));
            await _deletionService
                .DeleteFilesAsync(request, ct)
                .ConfigureAwait(false);
        }

        List<GalleryItemState> retainedItems = state.Items
            .Where(item => !HasMissingGeneratedImage(item))
            .ToList();
        IReadOnlyList<GalleryItemState> normalizedItems =
            _timestampNormalizer.Normalize(retainedItems);
        int galleryOrderChangeCount = normalizedItems
            .Where((item, index) =>
                item.GalleryOrderTimestampUtc
                != retainedItems[index].GalleryOrderTimestampUtc)
            .Count();
        bool galleryOrderChanged = galleryOrderChangeCount > 0;

        await _fileOrderSynchronizer
            .SynchronizeAsync(normalizedItems, ct)
            .ConfigureAwait(false);

        if (missingImageItems.Count == 0 && !galleryOrderChanged)
        {
            return;
        }

        GalleryState reconciledState = new()
        {
            Items = normalizedItems
        };
        await _stateStore
            .SaveAsync(_section, reconciledState, ct)
            .ConfigureAwait(false);
        if (missingImageItems.Count > 0)
        {
            _logger.LogInformation(
                "Removed {ItemCount} gallery items whose generated image files are missing.",
                missingImageItems.Count);
        }

        if (galleryOrderChanged)
        {
            _logger.LogInformation(
                "Initialized or repaired gallery file ordering metadata for {ItemCount} items.",
                galleryOrderChangeCount);
        }
    }

    private bool HasMissingGeneratedImage(GalleryItemState item)
    {
        return item.Status == GenerationItemStatus.Generated
            && _pathConverter.IsStoredImageFileMissing(item.ImagePath);
    }
}
