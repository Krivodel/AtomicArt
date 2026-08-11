using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AtomicArt.Desktop.ViewModels.Gallery;

internal sealed class GallerySelectionController : IDisposable
{
    public bool IsActive { get; private set; }
    public int SelectedCount { get; private set; }

    public event EventHandler? StateChanged;

    private readonly ReadOnlyObservableCollection<GenerationItemViewModel> _items;
    private readonly INotifyCollectionChanged _observableItems;
    private GenerationItemViewModel? _anchor;

    public GallerySelectionController(
        ReadOnlyObservableCollection<GenerationItemViewModel> items)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _observableItems = items;
        _observableItems.CollectionChanged += OnItemsCollectionChanged;
    }

    public void Exit()
    {
        if (!IsActive && SelectedCount == 0)
        {
            return;
        }

        ClearSelectedItems();
        _anchor = null;
        IsActive = false;
        NotifyStateChanged();
    }

    public void Toggle(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Contains(item))
        {
            return;
        }

        Activate();
        bool isSelected = !item.IsSelected;
        item.IsSelected = isSelected;
        SelectedCount += isSelected ? 1 : -1;
        _anchor = item;
        DeactivateWhenSelectionIsEmpty();
        NotifyStateChanged();
    }

    public void SelectRange(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Contains(item))
        {
            return;
        }

        Activate();
        int targetIndex = _items.IndexOf(item);
        int anchorIndex = _anchor is null
            ? -1
            : _items.IndexOf(_anchor);

        if (anchorIndex < 0)
        {
            item.IsSelected = true;
            _anchor = item;
            RecalculateSelectedCount();
            NotifyStateChanged();
            return;
        }

        int firstIndex = Math.Min(anchorIndex, targetIndex);
        int lastIndex = Math.Max(anchorIndex, targetIndex);

        for (int index = firstIndex; index <= lastIndex; index++)
        {
            _items[index].IsSelected = true;
        }

        RecalculateSelectedCount();
        NotifyStateChanged();
    }

    public void SelectAll()
    {
        if (_items.Count == 0)
        {
            return;
        }

        Activate();

        foreach (GenerationItemViewModel item in _items)
        {
            item.IsSelected = true;
        }

        _anchor = _items[0];
        RecalculateSelectedCount();
        NotifyStateChanged();
    }

    public IReadOnlyList<GenerationItemViewModel> GetSelectedItems()
    {
        return _items
            .Where(item => item.IsSelected)
            .ToList();
    }

    public void Dispose()
    {
        _observableItems.CollectionChanged -= OnItemsCollectionChanged;
    }

    private void ClearSelectedItems()
    {
        foreach (GenerationItemViewModel item in _items)
        {
            item.IsSelected = false;
        }

        SelectedCount = 0;
    }

    private void RecalculateSelectedCount()
    {
        SelectedCount = _items.Count(item => item.IsSelected);
    }

    private void Activate()
    {
        if (IsActive)
        {
            return;
        }

        ClearSelectedItems();
        _anchor = null;
        IsActive = true;
    }

    private void DeactivateWhenSelectionIsEmpty()
    {
        if (SelectedCount > 0)
        {
            return;
        }

        _anchor = null;
        IsActive = false;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if ((_anchor is not null) && !_items.Contains(_anchor))
        {
            _anchor = null;
        }

        RecalculateSelectedCount();
        DeactivateWhenSelectionIsEmpty();
        NotifyStateChanged();
    }
}
