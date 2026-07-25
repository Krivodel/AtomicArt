namespace AtomicArt.Desktop.Services.Paths;

internal static class DataRootPathRebaser
{
    internal static string? RebaseOrOriginal(
        string? path,
        string sourceRootDirectory,
        string destinationRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        string sourceRoot = Path.GetFullPath(sourceRootDirectory);
        string destinationRoot = Path.GetFullPath(destinationRootDirectory);
        string fullPath = Path.GetFullPath(path);

        if (!TrustedPathGuard.IsInsideDirectory(sourceRoot, fullPath))
        {
            return path;
        }

        string relativePath = Path.GetRelativePath(sourceRoot, fullPath);

        return Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
    }
}
