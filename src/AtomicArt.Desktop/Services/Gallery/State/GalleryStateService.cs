using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateService : IGalleryStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IGenerationReferenceStore _referenceStore;
    private readonly IGenerationChatStore _chats;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly GalleryStatePathConverter _pathConverter;
    private readonly GalleryStateSection _section;
    private readonly ILogger<GalleryStateService> _logger;
    private readonly SemaphoreSlim _stateLock;
    private GalleryState? _currentState;

    public GalleryStateService(
        IAppStateStore stateStore,
        IGenerationReferenceStore referenceStore,
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GalleryStateSection section,
        ILogger<GalleryStateService> logger,
        IGenerationChatStore chats)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _chats = chats ?? throw new ArgumentNullException(nameof(chats));
        _referenceStore = referenceStore ?? throw new ArgumentNullException(nameof(referenceStore));
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
            GalleryState normalizedState;

            using (DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false))
            {
                normalizedState = NormalizeRestoredState(loadedState);
            }

            if (loadedState.HasSavedPayload)
            {
                await _chats.PruneAsync(GalleryStateService.GetChatIds(normalizedState), ct).ConfigureAwait(false);
                await _referenceStore.PruneAsync(GetReferenceHashes(normalizedState), ct).ConfigureAwait(false);
            }

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

    public Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        return SaveLatestAsync(_ => Task.FromResult(items), ct);
    }

    public async Task SaveLatestAsync(Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>> getItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(getItems);
        await _stateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            IReadOnlyList<GalleryItemState> items = await getItems(ct).ConfigureAwait(false);
            GalleryState runtimeState;
            GalleryState storageState;

            using (DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false))
            {
                runtimeState = NormalizeRuntimeState(new GalleryState { Items = items.ToList() });
                storageState = CreateStorageState(runtimeState);
            }

            foreach (GalleryItemState item in runtimeState.Items)
            {
                if (item.ChatId is Guid id && item.ChatHistory is { } history)
                {
                    await _chats.SaveAsync(id, history, ct).ConfigureAwait(false);
                }
            }

            await _stateStore.SaveAsync(_section, storageState, ct).ConfigureAwait(false);
            await _chats.PruneAsync(GalleryStateService.GetChatIds(runtimeState), CancellationToken.None).ConfigureAwait(false);
            HashSet<string> previousHashes = GetReferenceHashes(_currentState);
            HashSet<string> retainedHashes = GetReferenceHashes(runtimeState);
            _currentState = runtimeState;

            if (!previousHashes.IsSubsetOf(retainedHashes))
            {
                await _referenceStore.PruneAsync(retainedHashes, CancellationToken.None).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Saved gallery state snapshot with {ItemCount} items",
                storageState.Items.Count);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private static HashSet<Guid> GetChatIds(GalleryState? state)
    {
        return (state?.Items ?? Array.Empty<GalleryItemState>()).Select(item => item.ChatId).OfType<Guid>().ToHashSet();
    }

    private static HashSet<string> GetReferenceHashes(GalleryState? state)
    {
        return (state?.Items ?? Array.Empty<GalleryItemState>()).SelectMany(GetReferenceHashes)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetReferenceHashes(GalleryItemState item)
    {
        IEnumerable<string> hashes = (item.References ?? Array.Empty<GenerationReference>()).Select(reference => reference.Hash)
            .Concat(item.ChatHistory?.ReferenceHashes ?? item.ChatReferenceHashes ?? Array.Empty<string>());

        return item.ChatHistory?.IncludesCardTurn != true && item.ChatResult?.Image is { } result
            ? hashes.Append(result.Hash)
            : hashes;
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
