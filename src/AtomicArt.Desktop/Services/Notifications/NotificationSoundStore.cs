using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Notifications;

public sealed class NotificationSoundStore : INotificationSoundStore
{
    public bool HasCustomSound => File.Exists(SoundPath);

    private const int MaximumFileBytes = 16 * 1024 * 1024;
    private const int ReadBufferSize = 4096;
    private const string ManagedPathFailure = "The notification sound must stay inside its data directory.";

    private string SoundPath => Path.Combine(_paths.RootDirectory, "Notifications", "Sound");

    private readonly IAtomicArtDataPathProvider _paths;
    private readonly IDataRootAccessCoordinator _access;

    public NotificationSoundStore(IAtomicArtDataPathProvider paths, IDataRootAccessCoordinator access)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public async Task ImportAsync(string sourcePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        byte[] content = await ReadFileAsync(sourcePath, ct).ConfigureAwait(false);
        await Task.Run(() => NotificationSoundDecoder.Validate(content), ct).ConfigureAwait(false);

        using DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false);
        string directory = Path.Combine(_paths.RootDirectory, "Notifications");
        TrustedPathGuard.EnsureTrustedDirectoryExists(directory,
            path => Directory.CreateDirectory(path), ManagedPathFailure);
        TrustedPathGuard.EnsureTrustedWriteTarget(directory, SoundPath, ManagedPathFailure);
        await AtomicBootstrapFileWriter.WriteAsync(directory, SoundPath, "sound", content, ct)
            .ConfigureAwait(false);
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        using DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false);
        string directory = Path.Combine(_paths.RootDirectory, "Notifications");
        TrustedPathGuard.DeleteTrustedFileIfExists(SoundPath, new[] { directory }, _paths.RootDirectory,
            path => TrustedPathGuard.EnsureInsideDirectory(directory, path, ManagedPathFailure));
    }

    public async Task<byte[]?> ReadAsync(CancellationToken ct)
    {
        using DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false);

        if (!File.Exists(SoundPath))
        {
            return null;
        }

        try
        {
            string directory = Path.Combine(_paths.RootDirectory, "Notifications");
            TrustedPathGuard.EnsureTrustedWriteTarget(directory, SoundPath, ManagedPathFailure);
            return await ReadFileAsync(SoundPath, ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadFileAsync(string path, CancellationToken ct)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, ReadBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length is <= 0 or > MaximumFileBytes)
        {
            throw new NotificationSoundException();
        }

        byte[] content = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(content, ct).ConfigureAwait(false);

        return content;
    }
}
