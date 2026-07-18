using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests;

internal sealed class StubAppStateStore : IAppStateStore
{
    private readonly object _state;

    internal StubAppStateStore(object state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Task<TState> LoadAsync<TState>(IStateSection section, CancellationToken ct)
    {
        if (_state is TState typedState)
        {
            return Task.FromResult(typedState);
        }

        throw new InvalidOperationException("Unexpected test state type.");
    }

    public Task SaveAsync<TState>(
        IStateSection section,
        TState state,
        CancellationToken ct)
        where TState : notnull
    {
        return SaveAsync(section, (object)state, ct);
    }

    public Task SaveAsync(IStateSection section, object state, CancellationToken ct)
    {
        throw new NotSupportedException("Direct saving is not used by this test.");
    }
}
