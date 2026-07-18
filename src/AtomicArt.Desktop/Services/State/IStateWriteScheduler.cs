namespace AtomicArt.Desktop.Services.State;

public interface IStateWriteScheduler
{
    void ScheduleWrite<TState>(
        IStateSection section,
        TState state,
        StateWriteMode mode = StateWriteMode.Deferred)
        where TState : notnull;

    Task FlushAsync(CancellationToken ct);
}
