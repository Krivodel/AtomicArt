using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Notifications;

public sealed class GenerationNotificationService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, string> _prompts = [];
    private readonly GenerationNotificationSettings _settings;
    private readonly IGenerationNotificationPresenter _presenter;
    private readonly ILogger<GenerationNotificationService> _logger;
    private readonly IDisposable _subscription;
    private CancellationTokenSource _presentationLifetime = new();
    private bool _disposed;

    public GenerationNotificationService(
        IGenerationLifecycleEventHub events,
        GenerationNotificationSettings settings,
        IGenerationNotificationPresenter presenter,
        ILogger<GenerationNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscription = events.Subscribe(OnLifecycleEvent);
        _settings.ValueChanged += OnSettingsChanged;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _prompts.Clear();
            _presentationLifetime.Cancel();
            _presentationLifetime.Dispose();
        }

        _subscription.Dispose();
        _settings.ValueChanged -= OnSettingsChanged;
    }

    private static GenerationNotification[] CreateNotifications(GenerationLifecycleEvent lifecycleEvent, string prompt)
    {
        if (lifecycleEvent.Batch is { Items.Count: > 0 } batch)
        {
            return batch.Items.Select(item => new GenerationNotification(
                item.Prompt, item.Status == GenerationItemStatus.Failed, lifecycleEvent.FailureCode)).ToArray();
        }

        return new[] { new GenerationNotification(prompt,
            lifecycleEvent.Status != GenerationLifecycleStatus.Completed, lifecycleEvent.FailureCode) };
    }

    private async Task PresentAsync(IReadOnlyList<GenerationNotification> notifications, CancellationToken ct)
    {
        try
        {
            Task[] presentations = notifications.Select(notification => _presenter.ShowAsync(notification, ct)).ToArray();
            await Task.WhenAll(presentations).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not display a generation notification.");
        }
    }

    private void OnLifecycleEvent(GenerationLifecycleEvent lifecycleEvent)
    {
        CancellationToken ct;
        string prompt;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            if (lifecycleEvent.Status is GenerationLifecycleStatus.StartRequested or GenerationLifecycleStatus.Started)
            {
                if (lifecycleEvent.Start is { } start)
                {
                    _prompts[lifecycleEvent.CorrelationId] = start.Prompt;
                }

                return;
            }

            if (!_prompts.Remove(lifecycleEvent.CorrelationId, out string? savedPrompt)
                || lifecycleEvent.IsCanceled || !_settings.Enabled)
            {
                return;
            }

            prompt = savedPrompt;
            ct = _presentationLifetime.Token;
        }

        _ = PresentAsync(CreateNotifications(lifecycleEvent, prompt), ct);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _presentationLifetime.Cancel();
            _presentationLifetime.Dispose();
            _presentationLifetime = new CancellationTokenSource();
        }
    }
}
