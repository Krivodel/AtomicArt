using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests;

internal sealed class TestUiFrameScheduler : IUiFrameScheduler
{
    public bool HasQueuedFrame => _frameActions.Count > 0;
    public int RequestedFrameCount => _requestedFrameCount;

    private readonly Queue<Action<TimeSpan>> _frameActions = [];
    private int _requestedFrameCount;

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        _requestedFrameCount++;
        _frameActions.Enqueue(frameAction);
    }

    public void RunNextFrame(TimeSpan frameTime)
    {
        Action<TimeSpan> frameAction = _frameActions.Dequeue();

        frameAction(frameTime);
    }

    public void RunFrame(TimeSpan frameTime)
    {
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
