using System.Text.Json;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Paths;

public sealed class AtomicArtDataRootBootstrapStore
{
    private const string BootstrapDirectoryName = "AtomicArt.Bootstrap";
    private const string StateFileName = "storage.json";

    private static readonly JsonSerializerOptions SerializerOptions =
        JsonFileSerializerOptions.Create();

    private readonly string _bootstrapDirectory;
    private readonly string _defaultRootDirectory;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal string BootstrapDirectory => _bootstrapDirectory;

    public AtomicArtDataRootBootstrapStore()
        : this(GetDefaultBootstrapDirectory(), GetDefaultRootDirectory())
    {
    }

    internal AtomicArtDataRootBootstrapStore(string bootstrapDirectory)
        : this(bootstrapDirectory, GetDefaultRootDirectory())
    {
    }

    internal AtomicArtDataRootBootstrapStore(
        string bootstrapDirectory,
        string defaultRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRootDirectory);

        _bootstrapDirectory = Path.GetFullPath(bootstrapDirectory);
        _defaultRootDirectory = Path.GetFullPath(defaultRootDirectory);
        _stateFilePath = Path.Combine(_bootstrapDirectory, StateFileName);
    }

    public static string GetDefaultRootDirectory()
    {
        return Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AtomicArtPathNames.RootDirectory));
    }

    public static string LoadConfiguredRootDirectory()
    {
        AtomicArtDataRootBootstrapStore store = new();

        return store.LoadRootDirectory();
    }

    public string LoadRootDirectory()
    {
        AtomicArtDataRootBootstrapState? state = LoadState();

        return state is null
            ? _defaultRootDirectory
            : Path.GetFullPath(state.RootDirectory);
    }

    public async Task SaveRootDirectoryAsync(string rootDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        AtomicArtDataRootBootstrapState? currentState = LoadState();
        AtomicArtDataRootBootstrapState state = new()
        {
            RootDirectory = Path.GetFullPath(rootDirectory),
            IsInitialRootDirectorySelectionCompleted =
                currentState?.IsInitialRootDirectorySelectionCompleted
        };

        await SaveStateAsync(state, ct).ConfigureAwait(false);
    }

    internal async Task MarkInitialRootDirectorySelectionPendingAsync(
        string rootDirectory,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        AtomicArtDataRootBootstrapState state = new()
        {
            RootDirectory = Path.GetFullPath(rootDirectory),
            IsInitialRootDirectorySelectionCompleted = false
        };

        await SaveStateAsync(state, ct).ConfigureAwait(false);
    }

    internal async Task MarkInitialRootDirectorySelectionCompletedAsync(
        string rootDirectory,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        AtomicArtDataRootBootstrapState state = new()
        {
            RootDirectory = Path.GetFullPath(rootDirectory),
            IsInitialRootDirectorySelectionCompleted = true
        };

        await SaveStateAsync(state, ct).ConfigureAwait(false);
    }

    internal bool ShouldOfferInitialRootDirectorySelection()
    {
        AtomicArtDataRootBootstrapState? state = LoadState();

        if (state is not null)
        {
            return state.IsInitialRootDirectorySelectionCompleted == false;
        }

        return !Directory.Exists(_defaultRootDirectory);
    }

    private AtomicArtDataRootBootstrapState? LoadState()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        string json = File.ReadAllText(_stateFilePath);
        AtomicArtDataRootBootstrapState? state =
            JsonSerializer.Deserialize<AtomicArtDataRootBootstrapState>(
                json,
                SerializerOptions);

        if (state is null || string.IsNullOrWhiteSpace(state.RootDirectory))
        {
            throw new InvalidDataException(
                "The Atomic Art data root bootstrap state is invalid.");
        }

        return state;
    }

    private async Task SaveStateAsync(
        AtomicArtDataRootBootstrapState state,
        CancellationToken ct)
    {
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await AtomicBootstrapFileWriter.WriteAsync(
                _bootstrapDirectory,
                _stateFilePath,
                "storage",
                content,
                ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string GetDefaultBootstrapDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            BootstrapDirectoryName);
    }
}
