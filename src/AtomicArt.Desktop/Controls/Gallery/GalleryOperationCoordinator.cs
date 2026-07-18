using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryOperationCoordinator : IAnimatedGalleryOperations
{
    internal IReadOnlyList<object> Items => RequireAttached(_items).ToList();
    internal ScrollViewer ScrollViewer => RequireAttached(_scrollViewer);
    internal Canvas GalleryPanel => RequireAttached(_galleryPanel);
    internal Canvas OverlayCanvas => RequireAttached(_overlayCanvas);
    internal Dictionary<Guid, Control> CardControls { get; } = [];
    internal HashSet<Guid> HiddenItemIds { get; } = [];

    private readonly GalleryLayoutService _galleryLayout;
    private readonly GalleryOperationQueueProcessor _operationQueue;
    private IList<object>? _items;
    private ScrollViewer? _scrollViewer;
    private Canvas? _galleryPanel;
    private Canvas? _overlayCanvas;
    private Func<object, Guid>? _itemIdSelector;
    private Func<object, Control>? _controlFactory;
    private Func<Task>? _waitForLayoutAsync;
    private Action? _stateChanged;

    public GalleryOperationCoordinator(
        IUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry runnerRegistry,
        GalleryLayoutService galleryLayout,
        GalleryOperationQueueProcessor operationQueue)
    {
        ArgumentNullException.ThrowIfNull(frameScheduler);
        ArgumentNullException.ThrowIfNull(runnerRegistry);
        _galleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        _operationQueue = operationQueue ?? throw new ArgumentNullException(nameof(operationQueue));
    }

    public Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        GalleryOperation operation = new AppendBatchGalleryOperation(items);

        return _operationQueue.EnqueueAsync(operation, this, ct);
    }

    public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        GalleryOperation operation = new GenerateFrontGalleryOperation(items);

        return _operationQueue.EnqueueAsync(operation, this, ct);
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        GalleryOperation operation = new RemoveGalleryOperation(itemId);

        return _operationQueue.EnqueueAsync(operation, this, ct);
    }

    public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);

        GalleryOperation operation = new MixedMutationGalleryOperation(finalItems);

        return _operationQueue.EnqueueAsync(operation, this, ct);
    }

    public async Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);
        ct.ThrowIfCancellationRequested();

        HiddenItemIds.Clear();
        ReplaceItems(finalItems);
        _galleryLayout.RenderCards(this);
        await WaitForLayoutAsync();
    }

    internal void AttachScene(
        ScrollViewer scrollViewer,
        Canvas galleryPanel,
        Canvas overlayCanvas,
        IList<object> items,
        Func<object, Guid>? itemIdSelector = null,
        Func<object, Control>? controlFactory = null,
        Func<Task>? waitForLayoutAsync = null,
        Action? stateChanged = null)
    {
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _galleryPanel = galleryPanel ?? throw new ArgumentNullException(nameof(galleryPanel));
        _overlayCanvas = overlayCanvas ?? throw new ArgumentNullException(nameof(overlayCanvas));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemIdSelector = itemIdSelector ?? MissingItemIdSelector;
        _controlFactory = controlFactory ?? DefaultControlFactory;
        _waitForLayoutAsync = waitForLayoutAsync ?? WaitForLayoutCoreAsync;
        _stateChanged = stateChanged;
    }

    internal Guid GetItemId(object item)
    {
        EnsureSceneAttached();
        ArgumentNullException.ThrowIfNull(item);
        if (_itemIdSelector is null)
        {
            throw new InvalidOperationException("Gallery scene has not been attached to operation coordinator.");
        }

        return _itemIdSelector(item);
    }

    internal Control CreateControl(object item)
    {
        EnsureSceneAttached();
        ArgumentNullException.ThrowIfNull(item);
        if (_controlFactory is null)
        {
            throw new InvalidOperationException("Gallery scene has not been attached to operation coordinator.");
        }

        Control control = _controlFactory(item);
        control.DataContext = item;

        return control;
    }

    internal void AddItemsToEnd(IReadOnlyList<object> items)
    {
        EnsureSceneAttached();
        ArgumentNullException.ThrowIfNull(items);
        IList<object> galleryItems = GetMutableItems();

        foreach (object item in items)
        {
            galleryItems.Add(item);
        }
    }

    internal void InsertItemsAtStart(IReadOnlyList<object> items)
    {
        EnsureSceneAttached();
        ArgumentNullException.ThrowIfNull(items);
        IList<object> galleryItems = GetMutableItems();

        for (int i = items.Count - 1; i >= 0; i--)
        {
            galleryItems.Insert(0, items[i]);
        }
    }

    internal void ReplaceItems(IReadOnlyList<object> items)
    {
        EnsureSceneAttached();
        ArgumentNullException.ThrowIfNull(items);
        IList<object> galleryItems = GetMutableItems();

        galleryItems.Clear();
        foreach (object item in items)
        {
            galleryItems.Add(item);
        }
    }

    internal object? RemoveItem(Guid itemId)
    {
        EnsureSceneAttached();
        IList<object> galleryItems = GetMutableItems();

        for (int i = 0; i < galleryItems.Count; i++)
        {
            object item = galleryItems[i];
            if (GetItemId(item) != itemId)
            {
                continue;
            }

            galleryItems.RemoveAt(i);
            HiddenItemIds.Remove(itemId);
            return item;
        }

        return null;
    }

    internal async Task WaitForLayoutAsync()
    {
        EnsureSceneAttached();
        if (_waitForLayoutAsync is null)
        {
            throw new InvalidOperationException("Gallery scene has not been attached to operation coordinator.");
        }

        await _waitForLayoutAsync();
    }

    internal void NotifyStateChanged()
    {
        _stateChanged?.Invoke();
    }

    internal void EnsureSceneAttached()
    {
        if ((_items is null)
            || (_scrollViewer is null)
            || (_galleryPanel is null)
            || (_overlayCanvas is null)
            || (_itemIdSelector is null)
            || (_controlFactory is null)
            || (_waitForLayoutAsync is null))
        {
            throw new InvalidOperationException("Gallery scene has not been attached to operation coordinator.");
        }
    }

    private static Control DefaultControlFactory(object item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ContentControl();
    }

    private static Guid MissingItemIdSelector(object item)
    {
        ArgumentNullException.ThrowIfNull(item);

        throw new InvalidOperationException("Gallery item identifier selector was not provided.");
    }

    internal List<GalleryOperation> DrainLeadingOperations(Type operationType)
    {
        return _operationQueue.DrainLeadingOperations(operationType);
    }

    internal bool HasLeadingOperation(Type operationType)
    {
        return _operationQueue.HasLeadingOperation(operationType);
    }

    private IList<object> GetMutableItems()
    {
        return RequireAttached(_items);
    }

    private Task WaitForLayoutCoreAsync()
    {
        ScrollViewer.UpdateLayout();
        GalleryPanel.UpdateLayout();
        OverlayCanvas.UpdateLayout();

        return Task.CompletedTask;
    }

    private T RequireAttached<T>(T? value)
        where T : class
    {
        EnsureSceneAttached();

        return value ?? throw new InvalidOperationException("Gallery scene has not been attached to operation coordinator.");
    }
}
