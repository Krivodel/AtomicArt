using Microsoft.Extensions.Logging;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateService : IGalleryStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly GalleryStateSection _section;
    private readonly ILogger<GalleryStateService> _logger;
    private readonly SemaphoreSlim _stateLock;
    private GalleryState? _currentState;

    public GalleryStateService(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        ITrustedImageFileService trustedImageFileService,
        GalleryStateSection section,
        ILogger<GalleryStateService> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _trustedImageFileService = trustedImageFileService
            ?? throw new ArgumentNullException(nameof(trustedImageFileService));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateLock = new SemaphoreSlim(1, 1);
    }

    public async Task<GalleryState> LoadAsync(CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
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
            GalleryState nextState = NormalizeStorageState(
                new GalleryState
                {
                    Items = items.ToList()
                });
            _currentState = nextState;
            _writeScheduler.ScheduleWrite(_section, nextState);
            _logger.LogInformation(
                "Scheduled gallery state snapshot with {ItemCount} items",
                nextState.Items.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private GalleryState NormalizeStorageState(GalleryState state)
    {
        return NormalizeState(state, GalleryItemStateMapper.NormalizeForStorage);
    }

    private GalleryState NormalizeRestoredState(GalleryState state)
    {
        return NormalizeState(state, GalleryItemStateMapper.NormalizeForRestore);
    }

    private GalleryState NormalizeState(
        GalleryState state,
        Func<
            GalleryItemState,
            Func<GalleryItemState, string?>,
            Func<GalleryItemState, string?>,
            GalleryItemState> normalizeItem)
    {
        IReadOnlyList<GalleryItemState> items = state.Items ?? [];

        return new GalleryState
        {
            Items = items
                .Where(GalleryItemStateMapper.IsValid)
                .Select(item => normalizeItem(
                    item,
                    ResolveTrustedImagePath,
                    ResolveTrustedThumbnailPath))
                .ToList()
        };
    }

    private string? ResolveTrustedImagePath(GalleryItemState item)
    {
        return _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ImagePath,
            item.ModelId);
    }

    private string? ResolveTrustedThumbnailPath(GalleryItemState item)
    {
        return _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ThumbnailPath,
            item.ModelId);
    }
}
