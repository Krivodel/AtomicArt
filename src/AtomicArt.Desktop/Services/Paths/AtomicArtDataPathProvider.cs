namespace AtomicArt.Desktop.Services.Paths;

public sealed class AtomicArtDataPathProvider : IAtomicArtDataPathProvider
{
    private const string ArtDirectoryName = "Art";
    private const string LogsDirectoryName = "Logs";
    private const string SecretsDirectoryName = "Secrets";
    private const string ThumbnailsDirectoryName = "Thumbnails";
    private const string StateDirectoryName = "State";
    private const string StateAttachmentsDirectoryName = "Attachments";

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
        ArtDirectory = Path.GetFullPath(Path.Combine(RootDirectory, ArtDirectoryName));
        LogsDirectory = Path.GetFullPath(Path.Combine(RootDirectory, LogsDirectoryName));
        SecretsDirectory = Path.GetFullPath(Path.Combine(RootDirectory, SecretsDirectoryName));
        ThumbnailsDirectory = Path.GetFullPath(Path.Combine(RootDirectory, ThumbnailsDirectoryName));
        StateDirectory = Path.GetFullPath(Path.Combine(RootDirectory, StateDirectoryName));
        StateAttachmentsDirectory = Path.GetFullPath(
            Path.Combine(StateDirectory, StateAttachmentsDirectoryName));
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
