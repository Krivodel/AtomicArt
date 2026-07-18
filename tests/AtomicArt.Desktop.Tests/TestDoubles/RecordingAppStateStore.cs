using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingAppStateStore : AppStateStoreTestDouble
{
    private readonly object _syncRoot = new();
    private readonly HashSet<Type> _supportedStateTypes;
    private readonly List<object> _savedStates = [];

    public RecordingAppStateStore(params Type[] supportedStateTypes)
    {
        ArgumentNullException.ThrowIfNull(supportedStateTypes);

        _supportedStateTypes = new HashSet<Type>(supportedStateTypes);
    }

    public IReadOnlyList<TState> GetSavedStates<TState>()
    {
        lock (_syncRoot)
        {
            return _savedStates.OfType<TState>().ToList();
        }
    }

    public override Task<TState> LoadAsync<TState>(
        IStateSection section,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(section);

        throw new NotSupportedException("State loading is not used by this test.");
    }

    public override Task SaveAsync(
        IStateSection section,
        object state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(state);

        if (!_supportedStateTypes.Contains(state.GetType()))
        {
            throw new InvalidOperationException("Unexpected test state type.");
        }

        lock (_syncRoot)
        {
            _savedStates.Add(state);
        }

        return Task.CompletedTask;
    }
}
