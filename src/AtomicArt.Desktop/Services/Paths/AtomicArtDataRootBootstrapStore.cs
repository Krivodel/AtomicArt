using System.Text.Json;

namespace AtomicArt.Desktop.Services.Paths;

public sealed class AtomicArtDataRootBootstrapStore
{
    private const string BootstrapDirectoryName = "AtomicArt.Bootstrap";
    private const string StateFileName = "storage.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _bootstrapDirectory;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal string BootstrapDirectory => _bootstrapDirectory;

    public AtomicArtDataRootBootstrapStore()
        : this(GetDefaultBootstrapDirectory())
    {
    }

    internal AtomicArtDataRootBootstrapStore(string bootstrapDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapDirectory);

        _bootstrapDirectory = Path.GetFullPath(bootstrapDirectory);
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
        if (!File.Exists(_stateFilePath))
        {
            return GetDefaultRootDirectory();
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

        return Path.GetFullPath(state.RootDirectory);
    }

    public async Task SaveRootDirectoryAsync(string rootDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        AtomicArtDataRootBootstrapState state = new()
        {
            RootDirectory = Path.GetFullPath(rootDirectory)
        };
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
