namespace AtomicArt.Desktop.Services.State;

public interface IAppStateStore
{
    Task<TState> LoadAsync<TState>(IStateSection section, CancellationToken ct);

    Task SaveAsync<TState>(IStateSection section, TState state, CancellationToken ct)
        where TState : notnull;

    Task SaveAsync(IStateSection section, object state, CancellationToken ct);
}
