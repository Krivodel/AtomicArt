using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests;

internal sealed class RecordingStateWriteScheduler : IStateWriteScheduler
{
    public object? SavedState { get; private set; }
    public IStateSection? SavedSection { get; private set; }
    public StateWriteMode? SavedMode { get; private set; }
    public int CallCount { get; private set; }

    public void ScheduleWrite<TState>(
        IStateSection section,
        TState state,
        StateWriteMode mode = StateWriteMode.Deferred)
        where TState : notnull
    {
        CallCount++;
        SavedSection = section;
        SavedState = state;
        SavedMode = mode;
    }

    public Task FlushAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
