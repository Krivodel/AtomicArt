using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services;

public sealed class GenerationLifecycleEventHub : IGenerationLifecycleEventHub
{
    private readonly object _syncRoot = new();
    private readonly List<Action<GenerationLifecycleEvent>> _subscribers = [];
    private readonly ILogger<GenerationLifecycleEventHub> _logger;

    public GenerationLifecycleEventHub(ILogger<GenerationLifecycleEventHub> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public IDisposable Subscribe(Action<GenerationLifecycleEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        int subscriberCount;

        lock (_syncRoot)
        {
            _subscribers.Add(subscriber);
            subscriberCount = _subscribers.Count;
        }

        _logger.LogDebug(
            "Generation lifecycle subscriber registered; subscriber count is {SubscriberCount}.",
            subscriberCount);

        return new GenerationLifecycleSubscription(() => Unsubscribe(subscriber));
    }

    public void Publish(GenerationLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        Action<GenerationLifecycleEvent>[] subscribers;

        lock (_syncRoot)
        {
            subscribers = _subscribers.ToArray();
        }

        _logger.LogInformation(
            "Publishing generation lifecycle status {Status} for {CorrelationId} to {SubscriberCount} subscribers.",
            lifecycleEvent.Status,
            lifecycleEvent.CorrelationId,
            subscribers.Length);

        foreach (Action<GenerationLifecycleEvent> subscriber in subscribers)
        {
            NotifySubscriber(subscriber, lifecycleEvent);
        }
    }

    private void NotifySubscriber(
        Action<GenerationLifecycleEvent> subscriber,
        GenerationLifecycleEvent lifecycleEvent)
    {
        try
        {
            subscriber(lifecycleEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Generation lifecycle subscriber failed while handling {Status} for {CorrelationId}.",
                lifecycleEvent.Status,
                lifecycleEvent.CorrelationId);
        }
    }

    private void Unsubscribe(Action<GenerationLifecycleEvent> subscriber)
    {
        int subscriberCount;

        lock (_syncRoot)
        {
            _subscribers.Remove(subscriber);
            subscriberCount = _subscribers.Count;
        }

        _logger.LogDebug(
            "Generation lifecycle subscriber removed; subscriber count is {SubscriberCount}.",
            subscriberCount);
    }
}
