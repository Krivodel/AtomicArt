using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests;

internal sealed class StubAppStateStore : AppStateStoreTestDouble
{
    private readonly object _state;

    internal StubAppStateStore(object state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override Task<TState> LoadAsync<TState>(
        IStateSection section,
        CancellationToken ct)
    {
        if (_state is TState typedState)
        {
            return Task.FromResult(typedState);
        }

        throw new InvalidOperationException("Unexpected test state type.");
    }

    public override Task SaveAsync(
        IStateSection section,
        object state,
        CancellationToken ct)
    {
        throw new NotSupportedException("Direct saving is not used by this test.");
    }
}
