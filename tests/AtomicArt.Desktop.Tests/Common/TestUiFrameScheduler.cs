using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests;

internal sealed class TestUiFrameScheduler : TimeProvider, IUiFrameScheduler
{
    public bool HasQueuedFrame => _frameActions.Count > 0;
    public int RequestedFrameCount => _requestedFrameCount;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    private readonly Queue<Action<TimeSpan>> _frameActions = [];
    private int _requestedFrameCount;
    private TimeSpan _frameTime;

    public override long GetTimestamp()
    {
        return _frameTime.Ticks;
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        _requestedFrameCount++;
        _frameActions.Enqueue(frameAction);
    }

    public void RunNextFrame(TimeSpan frameTime)
    {
        _frameTime = frameTime;
        Action<TimeSpan> frameAction = _frameActions.Dequeue();

        frameAction(frameTime);
    }

    public void RunFrame(TimeSpan frameTime)
    {
        _frameTime = frameTime;
        int pendingActions = _frameActions.Count;
        for (int i = 0; i < pendingActions; i++)
        {
            RunNextFrame(frameTime);
        }
    }

    public async Task RunNextFrameAsync(TimeSpan frameTime)
    {
        RunNextFrame(frameTime);
        await RunQueuedContinuationsAsync();
    }

    public static async Task RunQueuedContinuationsAsync()
    {
        await Task.Run(static () => { });
    }
}
