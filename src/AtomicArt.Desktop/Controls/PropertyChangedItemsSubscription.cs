using System.ComponentModel;

namespace AtomicArt.Desktop.Controls;

internal sealed class PropertyChangedItemsSubscription<T>
    where T : class, INotifyPropertyChanged
{
    private readonly PropertyChangedEventHandler _handler;
    private readonly HashSet<T> _sources = [];

    internal PropertyChangedItemsSubscription(PropertyChangedEventHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    internal void ReplaceSources(IEnumerable<T>? sources)
    {
        Clear();

        if (sources is null)
        {
            return;
        }

        foreach (T source in sources)
        {
            if (_sources.Add(source))
            {
                source.PropertyChanged += _handler;
            }
        }
    }

    internal void Clear()
    {
        foreach (T source in _sources)
        {
            source.PropertyChanged -= _handler;
        }

        _sources.Clear();
    }
}
