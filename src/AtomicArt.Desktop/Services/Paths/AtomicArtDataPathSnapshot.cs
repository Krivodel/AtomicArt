namespace AtomicArt.Desktop.Services.Paths;

internal sealed class AtomicArtDataPathSnapshot
{
    internal string RootDirectory { get; }
    internal string ArtDirectory { get; }
    internal string LogsDirectory { get; }
    internal string LocalizationsDirectory { get; }
    internal string SecretsDirectory { get; }
    internal string ThumbnailsDirectory { get; }
    internal string StateDirectory { get; }
    internal string StateAttachmentsDirectory { get; }
    internal string ModulesDirectory { get; }

    private AtomicArtDataPathSnapshot(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ArtDirectory = AtomicArtDataPathSnapshot.CreateChildPath(rootDirectory, AtomicArtPathNames.ArtDirectory);
        LogsDirectory = AtomicArtDataPathSnapshot.CreateChildPath(rootDirectory, AtomicArtPathNames.LogsDirectory);
        LocalizationsDirectory = AtomicArtDataPathSnapshot.CreateChildPath(
            rootDirectory,
            AtomicArtPathNames.LocalizationsDirectory);
        SecretsDirectory = AtomicArtDataPathSnapshot.CreateChildPath(rootDirectory, AtomicArtPathNames.SecretsDirectory);
        ThumbnailsDirectory = AtomicArtDataPathSnapshot.CreateChildPath(
            rootDirectory,
            AtomicArtPathNames.ThumbnailsDirectory);
        StateDirectory = AtomicArtDataPathSnapshot.CreateChildPath(rootDirectory, AtomicArtPathNames.StateDirectory);
        StateAttachmentsDirectory = AtomicArtDataPathSnapshot.CreateChildPath(
            StateDirectory,
            AtomicArtPathNames.StateAttachmentsDirectory);
        ModulesDirectory = AtomicArtDataPathSnapshot.CreateChildPath(rootDirectory, AtomicArtPathNames.ModulesDirectory);
    }

    internal static AtomicArtDataPathSnapshot Create(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        string fullRootDirectory = Path.GetFullPath(rootDirectory);

        return new AtomicArtDataPathSnapshot(
            Path.TrimEndingDirectorySeparator(fullRootDirectory));
    }

    internal bool IsKnownDirectory(string fullPath)
    {
        return (string.Equals(fullPath, RootDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, ArtDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, LogsDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(
                fullPath,
                LocalizationsDirectory,
                StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, SecretsDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, ThumbnailsDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, StateDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(fullPath, ModulesDirectory, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(
                fullPath,
                StateAttachmentsDirectory,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateChildPath(string parentDirectory, string childDirectoryName)
    {
        return Path.GetFullPath(Path.Combine(parentDirectory, childDirectoryName));
    }
}
