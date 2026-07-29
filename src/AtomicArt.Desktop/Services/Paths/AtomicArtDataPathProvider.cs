namespace AtomicArt.Desktop.Services.Paths;

public sealed class AtomicArtDataPathProvider :
    IAtomicArtDataPathProvider,
    IAtomicArtDataPathSwitcher
{
    public string RootDirectory => Volatile.Read(ref _snapshot).RootDirectory;
    public string ArtDirectory => Volatile.Read(ref _snapshot).ArtDirectory;
    public string LogsDirectory => Volatile.Read(ref _snapshot).LogsDirectory;
    public string LocalizationsDirectory =>
        Volatile.Read(ref _snapshot).LocalizationsDirectory;
    public string SecretsDirectory => Volatile.Read(ref _snapshot).SecretsDirectory;
    public string ThumbnailsDirectory => Volatile.Read(ref _snapshot).ThumbnailsDirectory;
    public string StateDirectory => Volatile.Read(ref _snapshot).StateDirectory;
    public string StateAttachmentsDirectory =>
        Volatile.Read(ref _snapshot).StateAttachmentsDirectory;

    private AtomicArtDataPathSnapshot _snapshot;

    public AtomicArtDataPathProvider()
        : this(AtomicArtDataRootBootstrapStore.LoadConfiguredRootDirectory())
    {
    }

    public AtomicArtDataPathProvider(string rootDirectory)
    {
        _snapshot = AtomicArtDataPathSnapshot.Create(rootDirectory);
    }

    public void EnsureDirectoryExists(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        AtomicArtDataPathSnapshot snapshot = Volatile.Read(ref _snapshot);
        string fullPath = Path.GetFullPath(directoryPath);

        if (!snapshot.IsKnownDirectory(fullPath))
        {
            throw new InvalidOperationException(
                "AtomicArt data path provider can create only known AtomicArt data directories.");
        }

        Directory.CreateDirectory(fullPath);
    }

    public void SwitchRootDirectory(string rootDirectory)
    {
        AtomicArtDataPathSnapshot snapshot = AtomicArtDataPathSnapshot.Create(rootDirectory);
        Interlocked.Exchange(ref _snapshot, snapshot);
    }
}
