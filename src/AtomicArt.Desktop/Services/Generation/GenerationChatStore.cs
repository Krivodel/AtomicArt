using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationChatStore : IGenerationChatStore, IDisposable
{
    private readonly IAppStateStore _state;
    private readonly ILogger<GenerationChatStore> _logger;
    private readonly IAtomicArtDataPathProvider _paths;
    private readonly IDataRootAccessCoordinator _access;
    private readonly Dictionary<Guid, WeakReference<GenerationChatHistory>> _saved = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GenerationChatStore(IAppStateStore state, ILogger<GenerationChatStore> logger,
        IAtomicArtDataPathProvider paths, IDataRootAccessCoordinator access)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerationChatHistory> LoadAsync(Guid id, CancellationToken ct)
    {
        GenerationChatState state = await _state.LoadAsync<GenerationChatState>(new GenerationChatStateSection(id), ct)
            .ConfigureAwait(false);
        GenerationChatHistory history = state.History is { } savedHistory && savedHistory.Id == id
            ? savedHistory
            : throw new InvalidDataException("The saved chat history is unavailable.");
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            _saved[id] = new WeakReference<GenerationChatHistory>(history);
        }
        finally
        {
            _gate.Release();
        }

        return history;
    }

    public async Task SaveAsync(Guid id, GenerationChatHistory history, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (id != history.Id)
        {
            throw new ArgumentException("The chat identifier must match the history revision.", nameof(id));
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_saved.TryGetValue(id, out WeakReference<GenerationChatHistory>? saved)
                && saved.TryGetTarget(out GenerationChatHistory? previous) && object.ReferenceEquals(previous, history))
            {
                return;
            }

            await _state.SaveAsync(new GenerationChatStateSection(id), new GenerationChatState { History = history }, ct)
                                .ConfigureAwait(false);
            _saved[id] = new WeakReference<GenerationChatHistory>(history);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PruneAsync(IReadOnlySet<Guid> retainedIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retainedIds);
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            List<Guid> unused = [];

            using (DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false))
            {
                string directory = _paths.ChatsDirectory;
                TrustedPathGuard.EnsureTrustedWriteTarget(_paths.RootDirectory, directory, "The chat directory is unsafe.");

                if (Directory.Exists(directory))
                {
                    foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
                    {
                        if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id)
                            && !retainedIds.Contains(id))
                        {
                            unused.Add(id);
                        }
                    }
                }
            }

            foreach (Guid id in unused)
            {
                await _state.DeleteAsync(new GenerationChatStateSection(id), ct).ConfigureAwait(false);
                _saved.Remove(id);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not delete unused chat histories.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
