using Moq;

namespace AtomicArt.Desktop.Tests.Services.Notifications;

internal sealed class NotificationTestTimer
{
    public TimeSpan DueTime { get; }
    public ITimer Timer => _timer.Object;
    public Task Disposed => _disposed.Task;

    private readonly Mock<ITimer> _timer = new();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimerCallback _callback;
    private readonly object? _state;

    public NotificationTestTimer(TimerCallback callback, object? state, TimeSpan dueTime)
    {
        _callback = callback;
        _state = state;
        DueTime = dueTime;
        _timer.Setup(timer => timer.Dispose()).Callback(() => _disposed.TrySetResult());
    }

    public void Fire()
    {
        _callback(_state);
    }
}
