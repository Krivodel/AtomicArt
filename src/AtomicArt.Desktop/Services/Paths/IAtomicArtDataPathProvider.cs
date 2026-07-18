namespace AtomicArt.Desktop.Services.Paths;

public interface IAtomicArtDataPathProvider
{
    string RootDirectory { get; }
    string ArtDirectory { get; }
    string LogsDirectory { get; }
    string SecretsDirectory { get; }
    string ThumbnailsDirectory { get; }
    string StateDirectory { get; }
    string StateAttachmentsDirectory { get; }

    void EnsureDirectoryExists(string directoryPath);
}
