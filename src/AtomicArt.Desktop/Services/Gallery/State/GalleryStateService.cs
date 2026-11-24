using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateService : IGalleryStateService
{
    private readonly IAppStateStore _stateStore;
    private readonly IGenerationReferenceStore _referenceStore;
    private readonly IGenerationImageFormatRegistry _formats;
    private readonly IGenerationChatStore _chats;
    private readonly GalleryImageDeduplicationService _imageDeduplication;
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
        IGenerationChatStore chats,
        GalleryImageDeduplicationService imageDeduplication,
        IGenerationImageFormatRegistry formats)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _chats = chats ?? throw new ArgumentNullException(nameof(chats));
        _imageDeduplication = imageDeduplication ?? throw new ArgumentNullException(nameof(imageDeduplication));
        _referenceStore = referenceStore ?? throw new ArgumentNullException(nameof(referenceStore));
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
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
        Guid placementLease = Guid.NewGuid();

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
            await _referenceStore.PromoteAsync(placementLease, GetGalleryReferences(loadedState.Items), ct)
                .ConfigureAwait(false);

            using (DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false))
            {
                normalizedState = NormalizeRestoredState(loadedState);
            }

            if (loadedState.HasSavedPayload)
            {
                IReadOnlyList<GalleryImageDuplicate> duplicates = await _imageDeduplication.FindAsync(normalizedState.Items, ct)
                    .ConfigureAwait(false);

                if (duplicates.Count > 0)
                {
                    normalizedState = GalleryStateService.CreateSharedImageState(normalizedState, duplicates);
                }

                GalleryState storageState = CreateStorageState(normalizedState);

                if (duplicates.Count > 0 || !loadedState.Items.Select(item => item.ImagePath)
                    .SequenceEqual(storageState.Items.Select(item => item.ImagePath), StringComparer.OrdinalIgnoreCase))
                {
                    await _stateStore.SaveAsync(_section, storageState, ct).ConfigureAwait(false);
                    await _imageDeduplication.DeleteAsync(duplicates, CancellationToken.None).ConfigureAwait(false);
                }

                await _chats.PruneAsync(GalleryStateService.GetChatIds(normalizedState), ct).ConfigureAwait(false);
                await _referenceStore.SynchronizeAsync(GetGalleryHashes(normalizedState),
                    GalleryStateService.GetReferenceHashes(normalizedState), ct).ConfigureAwait(false);
            }

            _currentState = normalizedState;
            _logger.LogInformation(
                "Loaded and normalized gallery state with {ItemCount} items",
                normalizedState.Items.Count);

            return normalizedState;
        }
        finally
        {
            _referenceStore.Release(placementLease);
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
        Guid placementLease = Guid.NewGuid();

        try
        {
            IReadOnlyList<GalleryItemState> items = await getItems(ct).ConfigureAwait(false);
            await _referenceStore.PromoteAsync(placementLease, GetGalleryReferences(items), ct)
                .ConfigureAwait(false);
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
            _currentState = runtimeState;
            await _referenceStore.SynchronizeAsync(GetGalleryHashes(runtimeState),
                GalleryStateService.GetReferenceHashes(runtimeState), CancellationToken.None).ConfigureAwait(false);

            _logger.LogInformation(
                "Saved gallery state snapshot with {ItemCount} items",
                storageState.Items.Count);
        }
        finally
        {
            _referenceStore.Release(placementLease);
            _stateLock.Release();
        }
    }

    private static GalleryState CreateSharedImageState(GalleryState state, IReadOnlyList<GalleryImageDuplicate> duplicates)
    {
        Dictionary<string, string> replacements = duplicates.GroupBy(duplicate => duplicate.OriginalPath,
            StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().SharedPath,
                StringComparer.OrdinalIgnoreCase);
        GalleryState sharedState = new()
        {
            HasSavedPayload = state.HasSavedPayload,
            Items = state.Items.Select(item => GalleryItemStateMapper.NormalizeForStorage(item,
                source => source.ImagePath is { } path && replacements.TryGetValue(path, out string? sharedPath)
                    ? sharedPath
                    : source.ImagePath)).ToArray()
        };

        return sharedState;
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

        return item.ChatResult?.Image is { } result
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

    private HashSet<string> GetGalleryHashes(GalleryState state)
    {
        return GetGalleryReferences(state.Items).Select(reference => reference.Hash).ToHashSet(StringComparer.Ordinal);
    }

    private IReadOnlyList<GenerationReference> GetGalleryReferences(IReadOnlyList<GalleryItemState> items)
    {
        return items.Where(item => item.Status == GenerationItemStatus.Generated)
            .Select(GetGalleryReference).OfType<GenerationReference>()
            .DistinctBy(reference => reference.Hash).ToArray();
    }

    private GenerationReference? GetGalleryReference(GalleryItemState item)
    {
        if (item.ChatResult?.Image is { } reference && GenerationReferenceFilePolicy.IsHash(reference.Hash))
        {
            return reference;
        }

        if (!GenerationReferenceFilePolicy.IsHash(Path.GetFileNameWithoutExtension(item.ImagePath)))
        {
            return null;
        }

        string? path = _pathConverter.GetImagePathForDeletion(item.ImagePath);

        return path is not null && _formats.TryGetByFileName(path, out IGenerationImageFormat? format) && format is not null
            ? new GenerationReference(Path.GetFileNameWithoutExtension(path), Path.GetFileName(path), format.ContentType)
            : null;
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
