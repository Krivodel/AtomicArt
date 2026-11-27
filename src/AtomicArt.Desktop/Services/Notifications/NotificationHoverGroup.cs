namespace AtomicArt.Desktop.Services.Notifications;

internal sealed class NotificationHoverGroup
{
    private readonly object _syncRoot = new();
    private readonly HashSet<Guid> _hovered = [];
    private readonly TimeProvider _time;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _version;

    public NotificationHoverGroup(TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public bool CanExpire(long version)
    {
        lock (_syncRoot)
        {
            return (_version == version) && (_hovered.Count == 0);
        }
    }

    public void SetHovered(Guid id, bool hovered)
    {
        lock (_syncRoot)
        {
            bool changed = hovered ? _hovered.Add(id) : _hovered.Remove(id);

            if (!changed)
            {
                return;
            }

            _version++;
            TaskCompletionSource previous = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            previous.TrySetResult();
        }
    }

    public async Task<long> WaitForExpirationAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            long version;
            bool hovered;
            Task changed;

            lock (_syncRoot)
            {
                version = _version;
                hovered = _hovered.Count > 0;
                changed = _changed.Task;
            }

            if (hovered)
            {
                await changed.WaitAsync(ct).ConfigureAwait(false);
                continue;
            }

            using CancellationTokenSource delayLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                Task delay = Task.Delay(GenerationNotification.DisplayDuration, _time, delayLifetime.Token);
                Task completed = await Task.WhenAny(delay, changed).WaitAsync(ct).ConfigureAwait(false);

                if (ReferenceEquals(completed, delay))
                {
                    await delay.ConfigureAwait(false);
                    return version;
                }
            }
            finally
            {
                delayLifetime.Cancel();
            }
        }
    }
}
