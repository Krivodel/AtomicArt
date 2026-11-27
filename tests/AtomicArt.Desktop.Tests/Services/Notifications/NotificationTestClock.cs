using Moq;

namespace AtomicArt.Desktop.Tests.Services.Notifications;

internal sealed class NotificationTestClock
{
    public TimeProvider Time => _time.Object;
    public IReadOnlyList<NotificationTestTimer> Timers
    {
        get
        {
            lock (_syncRoot)
            {
                return _timers.ToArray();
            }
        }
    }

    private readonly object _syncRoot = new();
    private readonly Mock<TimeProvider> _time = new() { CallBase = true };
    private readonly List<NotificationTestTimer> _timers = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public NotificationTestClock()
    {
        _time.Setup(time => time.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Returns<TimerCallback, object?, TimeSpan, TimeSpan>((callback, state, due, _) =>
            {
                lock (_syncRoot)
                {
                    NotificationTestTimer timer = new(callback, state, due);
                    _timers.Add(timer);
                    TaskCompletionSource previous = _changed;
                    _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    previous.TrySetResult();
                    return timer.Timer;
                }
            });
    }

    public async Task WaitForTimerCountAsync(int count, CancellationToken ct)
    {
        while (true)
        {
            Task changed;

            lock (_syncRoot)
            {
                if (_timers.Count >= count)
                {
                    return;
                }

                changed = _changed.Task;
            }

            await changed.WaitAsync(ct).ConfigureAwait(false);
        }
    }
}
