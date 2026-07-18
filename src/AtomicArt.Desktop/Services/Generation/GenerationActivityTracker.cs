using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationActivityTracker : IGenerationActivityTracker
{
    public bool IsActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeActivityCount > 0;
            }
        }
    }

    public event EventHandler? ActivityChanged;

    private readonly ILogger<GenerationActivityTracker> _logger;
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, HashSet<GenerationActivityPhase>> _phasesByCorrelationId = [];
    private TaskCompletionSource _idleCompletionSource = CreateCompletedIdleSource();
    private int _activeActivityCount;

    public GenerationActivityTracker(ILogger<GenerationActivityTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Start(Guid correlationId, GenerationActivityPhase phase)
    {
        bool activityChanged = false;

        lock (_syncRoot)
        {
            if (!_phasesByCorrelationId.TryGetValue(
                    correlationId,
                    out HashSet<GenerationActivityPhase>? phases))
            {
                phases = [];
                _phasesByCorrelationId.Add(correlationId, phases);
            }

            if (phases.Add(phase))
            {
                if (_activeActivityCount == 0)
                {
                    _idleCompletionSource = CreatePendingIdleSource();
                }

                _activeActivityCount++;
                activityChanged = true;
            }
        }

        if (activityChanged)
        {
            NotifyActivityChanged();
        }
    }

    public void Complete(Guid correlationId, GenerationActivityPhase phase)
    {
        TaskCompletionSource? completedIdleSource = null;
        bool activityChanged = false;

        lock (_syncRoot)
        {
            if (_phasesByCorrelationId.TryGetValue(
                    correlationId,
                    out HashSet<GenerationActivityPhase>? phases)
                && phases.Remove(phase))
            {
                _activeActivityCount--;
                activityChanged = true;

                if (phases.Count == 0)
                {
                    _phasesByCorrelationId.Remove(correlationId);
                }

                if (_activeActivityCount == 0)
                {
                    completedIdleSource = _idleCompletionSource;
                }
            }
        }

        completedIdleSource?.TrySetResult();

        if (activityChanged)
        {
            NotifyActivityChanged();
        }
    }

    public Task WaitUntilIdleAsync(CancellationToken ct)
    {
        Task idleTask;

        lock (_syncRoot)
        {
            idleTask = _idleCompletionSource.Task;
        }

        return idleTask.WaitAsync(ct);
    }

    private static TaskCompletionSource CreateCompletedIdleSource()
    {
        TaskCompletionSource completionSource = CreatePendingIdleSource();
        completionSource.SetResult();

        return completionSource;
    }

    private static TaskCompletionSource CreatePendingIdleSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void NotifyActivityChanged()
    {
        EventHandler? activityChanged = ActivityChanged;

        if (activityChanged is null)
        {
            return;
        }

        foreach (Delegate subscriber in activityChanged.GetInvocationList())
        {
            try
            {
                ((EventHandler)subscriber)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generation activity subscriber failed.");
            }
        }
    }
}
