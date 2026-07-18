using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests;

internal sealed class RecordingStateWriteScheduler : IStateWriteScheduler
{
    public object? SavedState { get; private set; }

    public void ScheduleWrite<TState>(
        IStateSection section,
        TState state,
        StateWriteMode mode = StateWriteMode.Deferred)
        where TState : notnull
    {
        SavedState = state;
    }

    public Task FlushAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
