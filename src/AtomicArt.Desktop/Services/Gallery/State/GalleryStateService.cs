using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateService : IGalleryStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly GalleryStatePathConverter _pathConverter;
    private readonly GalleryStateSection _section;
    private readonly ILogger<GalleryStateService> _logger;
    private readonly SemaphoreSlim _stateLock;
    private GalleryState? _currentState;

    public GalleryStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GalleryStateSection section,
        ILogger<GalleryStateService> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _pathConverter = pathConverter ?? throw new ArgumentNullException(nameof(pathConverter));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateLock = new SemaphoreSlim(1, 1);
    }

    public async Task<GalleryState> LoadAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

            if (_currentState is not null)
            {
                _logger.LogDebug(
                    "Returning cached gallery state with {ItemCount} items",
                    _currentState.Items.Count);
                return _currentState;
            }

            GalleryState loadedState = await _stateStore
                .LoadAsync<GalleryState>(_section, ct)
                .ConfigureAwait(false);
            GalleryState normalizedState = NormalizeRestoredState(loadedState);
            _currentState = normalizedState;
            _logger.LogInformation(
                "Loaded and normalized gallery state with {ItemCount} items",
                normalizedState.Items.Count);

            return normalizedState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
            GalleryState runtimeState = NormalizeRuntimeState(
                new GalleryState
                {
                    Items = items.ToList()
                });
            GalleryState storageState = CreateStorageState(runtimeState);
            _currentState = runtimeState;
            _writeScheduler.ScheduleWrite(_section, storageState);
            _logger.LogInformation(
                "Scheduled gallery state snapshot with {ItemCount} items",
                storageState.Items.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private static GalleryState NormalizeState(
        GalleryState state,
        Func<
            GalleryItemState,
            Func<GalleryItemState, string?>,
            Func<GalleryItemState, string?>,
            GalleryItemState> normalizeItem,
        Func<GalleryItemState, string?> resolveImagePath,
        Func<GalleryItemState, string?> resolveThumbnailPath)
    {
        IReadOnlyList<GalleryItemState> items = state.Items ?? [];

        return new GalleryState
        {
            Items = items
                .Where(GalleryItemStateMapper.IsValid)
                .Select(item => normalizeItem(
                    item,
                    resolveImagePath,
                    resolveThumbnailPath))
                .ToList()
        };
    }

    private GalleryState NormalizeRuntimeState(GalleryState state)
    {
        return NormalizeState(
            state,
            GalleryItemStateMapper.NormalizeForStorage,
            ResolveValidatedImagePath,
            ResolveValidatedThumbnailPath);
    }

    private GalleryState CreateStorageState(GalleryState state)
    {
        return NormalizeState(
            state,
            GalleryItemStateMapper.NormalizeForStorage,
            ResolveStorageImagePath,
            ResolveStorageThumbnailPath);
    }

    private GalleryState NormalizeRestoredState(GalleryState state)
    {
        return NormalizeState(
            state,
            GalleryItemStateMapper.NormalizeForRestore,
            ResolveRuntimeImagePath,
            ResolveRuntimeThumbnailPath);
    }

    private string? ResolveRuntimeImagePath(GalleryItemState item)
    {
        return _pathConverter.GetRuntimeImagePath(
            item.ImagePath,
            item.ModelId);
    }

    private string? ResolveRuntimeThumbnailPath(GalleryItemState item)
    {
        return _pathConverter.GetRuntimeThumbnailPath(
            item.ThumbnailPath,
            item.ModelId);
    }

    private string? ResolveValidatedImagePath(GalleryItemState item)
    {
        return _pathConverter.GetValidatedRuntimePath(
            item.ImagePath,
            item.ModelId);
    }

    private string? ResolveValidatedThumbnailPath(GalleryItemState item)
    {
        return _pathConverter.GetValidatedRuntimePath(
            item.ThumbnailPath,
            item.ModelId);
    }

    private string? ResolveStorageImagePath(GalleryItemState item)
    {
        return _pathConverter.GetStoragePath(item.ImagePath);
    }

    private string? ResolveStorageThumbnailPath(GalleryItemState item)
    {
        return _pathConverter.GetStoragePath(item.ThumbnailPath);
    }
}
