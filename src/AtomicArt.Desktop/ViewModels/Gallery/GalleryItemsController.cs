using System.Collections.ObjectModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GalleryItemsController
{
    public ReadOnlyObservableCollection<GenerationItemViewModel> Items { get; }
    public bool IsEmpty => _items.Count == 0;
    public event EventHandler? IsEmptyChanged;

    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly IGenerationItemStatusDescriptorRegistry _statusDescriptorRegistry;
    private readonly GalleryOrderTimestampPolicy _timestampPolicy;
    private readonly ObservableCollection<GenerationItemViewModel> _items = [];

    public GalleryItemsController(
        ITrustedImageFileService trustedImageFileService,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry,
        GalleryOrderTimestampPolicy timestampPolicy)
    {
        ArgumentNullException.ThrowIfNull(trustedImageFileService);
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);
        ArgumentNullException.ThrowIfNull(timestampPolicy);

        _trustedImageFileService = trustedImageFileService;
        _statusDescriptorRegistry = statusDescriptorRegistry;
        _timestampPolicy = timestampPolicy;
        Items = new ReadOnlyObservableCollection<GenerationItemViewModel>(_items);
    }

    public IReadOnlyList<GenerationItemViewModel> CreateGeneratedItems(
        IReadOnlyList<GenerationItemDto> items,
        int attachedImagesCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        IReadOnlyList<GenerationItemDto> orderedItems = items
            .Select((item, index) => new IndexedGenerationItem(item, index))
            .OrderByDescending(item => item.Item.CreatedAtUtc)
            .ThenByDescending(item => item.Index)
            .Select(item => item.Item)
            .ToList();
        List<GenerationItemViewModel> addedItems = [];

        if (orderedItems.Count == 0)
        {
            return addedItems;
        }

        IReadOnlyList<DateTime> galleryOrderTimestamps =
            _timestampPolicy.CreateForPrependedItems(
                orderedItems[^1].CreatedAtUtc,
                GetCurrentNewestGalleryOrderTimestamp(),
                orderedItems.Count);

        for (int index = 0; index < orderedItems.Count; index++)
        {
            GenerationItemDto item = orderedItems[index];
            GenerationItemViewModel addedItem = CreateGeneratedItem(item, attachedImagesCount);
            addedItem.SetGalleryOrderTimestamp(
                galleryOrderTimestamps[index]);
            addedItems.Add(addedItem);
        }

        return addedItems;
    }

    public void AddGeneratedItems(IReadOnlyList<GenerationItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        bool wasEmpty = IsEmpty;
        int insertIndex = 0;

        foreach (GenerationItemViewModel item in items)
        {
            _items.Insert(insertIndex, item);
            insertIndex++;
        }

        NotifyIsEmptyChanged(wasEmpty);
    }

    public bool Contains(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _items.Contains(item);
    }

    public void Delete(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        bool wasEmpty = IsEmpty;
        _items.Remove(item);
        NotifyIsEmptyChanged(wasEmpty);
    }

    public IReadOnlyList<GenerationItemViewModel> GetItemsByCorrelationId(Guid correlationId)
    {
        return _items
            .Where(item => item.CorrelationId == correlationId)
            .OrderBy(item => item.GenerationOrdinal)
            .ToList();
    }

    public IReadOnlyList<GenerationItemViewModel> CreatePlaceholders(GenerationLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        if (lifecycleEvent.Start is null)
        {
            return [];
        }

        List<GenerationItemViewModel> placeholders = [];
        IReadOnlyList<DateTime> galleryOrderTimestamps =
            _timestampPolicy.CreateForPrependedItems(
                lifecycleEvent.Start.RequestedAtUtc,
                GetCurrentNewestGalleryOrderTimestamp(),
                lifecycleEvent.Start.GenerationCount);

        for (int index = 0; index < lifecycleEvent.Start.GenerationCount; index++)
        {
            GenerationItemViewModel placeholder = GenerationItemViewModel.CreatePlaceholder(
                lifecycleEvent.Start,
                lifecycleEvent.CorrelationId,
                index,
                galleryOrderTimestamps[index],
                _statusDescriptorRegistry);
            placeholders.Add(placeholder);
        }

        return placeholders;
    }

    public void AddPlaceholders(IReadOnlyList<GenerationItemViewModel> placeholders)
    {
        ArgumentNullException.ThrowIfNull(placeholders);

        bool wasEmpty = IsEmpty;
        int insertIndex = 0;

        foreach (GenerationItemViewModel placeholder in placeholders)
        {
            _items.Insert(insertIndex, placeholder);
            insertIndex++;
        }

        NotifyIsEmptyChanged(wasEmpty);
    }

    public void RemoveItemsByCorrelationId(Guid correlationId)
    {
        bool wasEmpty = IsEmpty;
        IReadOnlyList<GenerationItemViewModel> itemsToRemove = GetItemsByCorrelationId(correlationId);

        foreach (GenerationItemViewModel item in itemsToRemove)
        {
            _items.Remove(item);
        }

        NotifyIsEmptyChanged(wasEmpty);
    }

    public void MarkFailedByCorrelationId(
        Guid correlationId,
        string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        IReadOnlyList<GenerationItemViewModel> placeholders = GetItemsByCorrelationId(correlationId);

        foreach (GenerationItemViewModel placeholder in placeholders)
        {
            placeholder.MarkFailed(failureCode);
        }
    }

    public void MarkFailedPlaceholders(
        IReadOnlyList<GenerationItemViewModel> placeholders,
        int firstFailedIndex,
        string failureCode)
    {
        ArgumentNullException.ThrowIfNull(placeholders);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        for (int index = firstFailedIndex; index < placeholders.Count; index++)
        {
            placeholders[index].MarkFailed(failureCode);
        }
    }

    public void RefreshElapsedText(DateTime utcNow)
    {
        foreach (GenerationItemViewModel item in _items)
        {
            item.RefreshElapsedText(utcNow);
        }
    }

    public IReadOnlyList<object> GetItemsSnapshot()
    {
        return _items
            .Cast<object>()
            .ToList();
    }

    public IReadOnlyList<GalleryItemState> CreateStateSnapshot()
    {
        return _items
            .Select(item => item.CreateState())
            .ToList();
    }

    public void RestoreItems(IReadOnlyList<GalleryItemState> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        bool wasEmpty = IsEmpty;
        _items.Clear();

        foreach (GalleryItemState item in items)
        {
            GenerationItemViewModel viewModel = RestoreItem(item);
            _items.Add(viewModel);
        }

        NotifyIsEmptyChanged(wasEmpty);
    }

    public void RebaseDataRootPaths(
        string sourceRootDirectory,
        string destinationRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootDirectory);

        foreach (GenerationItemViewModel item in _items)
        {
            item.RebaseDataRootPaths(
                sourceRootDirectory,
                destinationRootDirectory);
        }
    }

    private GenerationItemViewModel CreateGeneratedItem(
        GenerationItemDto item,
        int attachedImagesCount)
    {
        string? trustedImagePath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ImagePath,
            item.ModelId);
        GenerationItemViewModel viewModel = new(
            item,
            attachedImagesCount,
            trustedImagePath,
            _statusDescriptorRegistry);

        return viewModel;
    }

    private GenerationItemViewModel RestoreItem(GalleryItemState item)
    {
        string? trustedImagePath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ImagePath,
            item.ModelId);
        string? trustedThumbnailPath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ThumbnailPath,
            item.ModelId);

        return GenerationItemViewModel.Restore(
            item,
            trustedImagePath,
            trustedThumbnailPath,
            _statusDescriptorRegistry);
    }

    private DateTime? GetCurrentNewestGalleryOrderTimestamp()
    {
        return _items.Count == 0
            ? null
            : _items
                .Select(item =>
                    item.GalleryOrderTimestampUtc ?? item.CreatedAtUtc)
                .Max();
    }

    private void NotifyIsEmptyChanged(bool wasEmpty)
    {
        if (wasEmpty != IsEmpty)
        {
            IsEmptyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed record IndexedGenerationItem(GenerationItemDto Item, int Index);
}
