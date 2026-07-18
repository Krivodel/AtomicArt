using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GalleryLifecycleViewStateController : IGalleryLifecycleViewState
{
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IAnimatedGalleryOperations _animatedGalleryOperations;
    private readonly GalleryItemsController _itemsController;

    public GalleryLifecycleViewStateController(
        IUiThreadDispatcher uiThreadDispatcher,
        IAnimatedGalleryOperations animatedGalleryOperations,
        GalleryItemsController itemsController)
    {
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(animatedGalleryOperations);
        ArgumentNullException.ThrowIfNull(itemsController);

        _uiThreadDispatcher = uiThreadDispatcher;
        _animatedGalleryOperations = animatedGalleryOperations;
        _itemsController = itemsController;
    }

    public async Task ApplyStartedAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        IReadOnlyList<GenerationItemViewModel> placeholders = [];

        await _uiThreadDispatcher.InvokeAsync(
            () =>
            {
                placeholders = _itemsController.CreatePlaceholders(lifecycleEvent);
                _itemsController.AddPlaceholders(placeholders);
            },
            ct);
        await _animatedGalleryOperations.GenerateFrontAsync(placeholders.Cast<object>().ToList(), ct);
    }

    public async Task ApplyCompletedAsync(
        Guid correlationId,
        IReadOnlyList<GalleryCompletedItemUpdate> itemUpdates,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(itemUpdates);

        await _uiThreadDispatcher.InvokeAsync(
            () => ApplyCompleted(correlationId, itemUpdates),
            ct);
    }

    public async Task ApplyStartFailedAsync(Guid correlationId, CancellationToken ct)
    {
        IReadOnlyList<object> finalItems = [];

        await _uiThreadDispatcher.InvokeAsync(
            () =>
            {
                _itemsController.RemoveItemsByCorrelationId(correlationId);
                finalItems = _itemsController.GetItemsSnapshot();
            },
            ct);
        await _animatedGalleryOperations.ApplyMixedMutationAsync(finalItems, ct);
    }

    public Task ApplyFailedAsync(Guid correlationId, CancellationToken ct)
    {
        return _uiThreadDispatcher.InvokeAsync(
            () => _itemsController.MarkFailedByCorrelationId(correlationId),
            ct);
    }

    public Task RefreshElapsedTextAsync(DateTime utcNow, CancellationToken ct)
    {
        return _uiThreadDispatcher.InvokeAsync(
            () => _itemsController.RefreshElapsedText(utcNow),
            ct);
    }

    public async Task<IReadOnlyList<GalleryItemState>> CreateStateSnapshotAsync(CancellationToken ct)
    {
        IReadOnlyList<GalleryItemState> snapshot = [];

        await _uiThreadDispatcher.InvokeAsync(
            () => snapshot = _itemsController.CreateStateSnapshot(),
            ct);

        return snapshot;
    }

    public async Task RestoreAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        await _uiThreadDispatcher.InvokeAsync(
            async () =>
            {
                _itemsController.RestoreItems(items);
                IReadOnlyList<object> restoredItems = _itemsController.GetItemsSnapshot();
                await _animatedGalleryOperations.RestoreSnapshotAsync(restoredItems, ct);
            },
            ct);
    }

    public Task GenerateFrontAsync(
        IReadOnlyList<GenerationItemViewModel> items,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        return _animatedGalleryOperations.GenerateFrontAsync(items.Cast<object>().ToList(), ct);
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        return _animatedGalleryOperations.RemoveAsync(itemId, ct);
    }

    private void ApplyCompleted(
        Guid correlationId,
        IReadOnlyList<GalleryCompletedItemUpdate> itemUpdates)
    {
        IReadOnlyList<GenerationItemViewModel> placeholders =
            _itemsController.GetItemsByCorrelationId(correlationId);
        int resultCount = Math.Min(placeholders.Count, itemUpdates.Count);

        for (int index = 0; index < resultCount; index++)
        {
            GalleryCompletedItemUpdate itemUpdate = itemUpdates[index];
            placeholders[index].UpdateFromResult(
                itemUpdate.Item,
                itemUpdate.TrustedImagePath,
                itemUpdate.ThumbnailPath);
        }

        _itemsController.MarkFailedPlaceholders(placeholders, resultCount);
    }
}
