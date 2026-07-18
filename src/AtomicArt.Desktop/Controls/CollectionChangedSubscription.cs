using System.Collections.Specialized;

namespace AtomicArt.Desktop.Controls;

internal sealed class CollectionChangedSubscription
{
    private readonly NotifyCollectionChangedEventHandler _handler;
    private INotifyCollectionChanged? _source;

    internal CollectionChangedSubscription(NotifyCollectionChangedEventHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    internal void ReplaceSource(object? source)
    {
        Clear();

        if (source is INotifyCollectionChanged collectionChanged)
        {
            _source = collectionChanged;
            _source.CollectionChanged += _handler;
        }
    }

    internal void Clear()
    {
        if (_source is not null)
        {
            _source.CollectionChanged -= _handler;
            _source = null;
        }
    }
}
