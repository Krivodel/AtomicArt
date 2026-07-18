namespace AtomicArt.Desktop.Services.Paths;

public sealed class AtomicArtDataPathProvider : IAtomicArtDataPathProvider
{
    public string RootDirectory { get; }
    public string ArtDirectory { get; }
    public string LogsDirectory { get; }
    public string SecretsDirectory { get; }
    public string ThumbnailsDirectory { get; }
    public string StateDirectory { get; }
    public string StateAttachmentsDirectory { get; }

    public AtomicArtDataPathProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AtomicArtPathNames.RootDirectory))
    {
    }

    public AtomicArtDataPathProvider(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        ArtDirectory = Path.GetFullPath(
            Path.Combine(RootDirectory, AtomicArtPathNames.ArtDirectory));
        LogsDirectory = Path.GetFullPath(
            Path.Combine(RootDirectory, AtomicArtPathNames.LogsDirectory));
        SecretsDirectory = Path.GetFullPath(
            Path.Combine(RootDirectory, AtomicArtPathNames.SecretsDirectory));
        ThumbnailsDirectory = Path.GetFullPath(
            Path.Combine(RootDirectory, AtomicArtPathNames.ThumbnailsDirectory));
        StateDirectory = Path.GetFullPath(
            Path.Combine(RootDirectory, AtomicArtPathNames.StateDirectory));
        StateAttachmentsDirectory = Path.GetFullPath(
            Path.Combine(StateDirectory, AtomicArtPathNames.StateAttachmentsDirectory));
    }

    public void EnsureDirectoryExists(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        string fullPath = Path.GetFullPath(directoryPath);

        if (!IsKnownDirectory(fullPath))
        {
            throw new InvalidOperationException(
                "AtomicArt data path provider can create only known AtomicArt data directories.");
        }

        Directory.CreateDirectory(fullPath);
    }

    private bool IsKnownDirectory(string fullPath)
    {
        return string.Equals(fullPath, RootDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, ArtDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, LogsDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, SecretsDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, ThumbnailsDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, StateDirectory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, StateAttachmentsDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
