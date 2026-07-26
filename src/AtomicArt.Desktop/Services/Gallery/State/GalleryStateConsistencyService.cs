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
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly GalleryStatePathConverter _pathConverter;
    private readonly GalleryStateSection _section;
    private readonly ILogger<GalleryStateConsistencyService> _logger;

    public GalleryStateConsistencyService(
        IAppStateStore stateStore,
        IGalleryItemDeletionService deletionService,
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GalleryStateSection section,
        ILogger<GalleryStateConsistencyService> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deletionService = deletionService
            ?? throw new ArgumentNullException(nameof(deletionService));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _pathConverter = pathConverter ?? throw new ArgumentNullException(nameof(pathConverter));
        _section = section ?? throw new ArgumentNullException(nameof(section));
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

        if (missingImageItems.Count == 0)
        {
            return;
        }

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
        GalleryState reconciledState = new()
        {
            Items = retainedItems
        };
        await _stateStore
            .SaveAsync(_section, reconciledState, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Removed {ItemCount} gallery items whose generated image files are missing.",
            missingImageItems.Count);
    }

    private bool HasMissingGeneratedImage(GalleryItemState item)
    {
        return item.Status == GenerationItemStatus.Generated
            && _pathConverter.IsStoredImageFileMissing(item.ImagePath);
    }
}
