using System.Text.Json;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Paths;

internal sealed class DataRootMigrationJournalStore
{
    private const string JournalFileName = "storage-migration.json";

    private static readonly JsonSerializerOptions SerializerOptions =
        JsonFileSerializerOptions.Create();

    private readonly string _bootstrapDirectory;
    private readonly string _journalFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DataRootMigrationJournalStore(
        AtomicArtDataRootBootstrapStore bootstrapStore)
    {
        ArgumentNullException.ThrowIfNull(bootstrapStore);

        _bootstrapDirectory = bootstrapStore.BootstrapDirectory;
        _journalFilePath = Path.Combine(_bootstrapDirectory, JournalFileName);
    }

    internal DataRootMigrationJournal? Load()
    {
        if (!File.Exists(_journalFilePath))
        {
            return null;
        }

        string json = File.ReadAllText(_journalFilePath);

        return JsonSerializer.Deserialize<DataRootMigrationJournal>(
            json,
            SerializerOptions);
    }

    internal async Task SaveAsync(DataRootMigrationJournal journal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(journal);

        byte[] content = JsonSerializer.SerializeToUtf8Bytes(journal, SerializerOptions);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await AtomicBootstrapFileWriter.WriteAsync(
                _bootstrapDirectory,
                _journalFilePath,
                "storage-migration",
                content,
                ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal void Delete()
    {
        FileDeletion.DeleteIfExists(_journalFilePath);
    }
}
