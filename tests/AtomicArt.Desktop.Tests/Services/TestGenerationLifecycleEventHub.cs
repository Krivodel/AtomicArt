using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

internal sealed class TestGenerationLifecycleEventHub : IGenerationLifecycleEventHub
{
    public IReadOnlyList<GenerationLifecycleEvent> PublishedEvents
    {
        get
        {
            lock (_syncRoot)
            {
                return _publishedEvents.ToList();
            }
        }
    }

    private readonly object _syncRoot = new();
    private readonly List<GenerationLifecycleEvent> _publishedEvents = [];
    private readonly List<Action<GenerationLifecycleEvent>> _subscribers = [];

    public IDisposable Subscribe(Action<GenerationLifecycleEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_syncRoot)
        {
            _subscribers.Add(subscriber);
        }

        return new TestSubscription(() => Unsubscribe(subscriber));
    }

    public void Publish(GenerationLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        Action<GenerationLifecycleEvent>[] subscribers;

        lock (_syncRoot)
        {
            _publishedEvents.Add(lifecycleEvent);
            subscribers = _subscribers.ToArray();
        }

        foreach (Action<GenerationLifecycleEvent> subscriber in subscribers)
        {
            subscriber(lifecycleEvent);
        }
    }

    private void Unsubscribe(Action<GenerationLifecycleEvent> subscriber)
    {
        lock (_syncRoot)
        {
            _subscribers.Remove(subscriber);
        }
    }
}
