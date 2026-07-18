using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests;

internal abstract class AppStateStoreTestDouble : IAppStateStore
{
    public abstract Task<TState> LoadAsync<TState>(
        IStateSection section,
        CancellationToken ct);

    public Task SaveAsync<TState>(
        IStateSection section,
        TState state,
        CancellationToken ct)
        where TState : notnull
    {
        return SaveAsync(section, (object)state, ct);
    }

    public abstract Task SaveAsync(
        IStateSection section,
        object state,
        CancellationToken ct);
}
