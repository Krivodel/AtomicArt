namespace AtomicArt.Tests.Common;

public static class TestRepositoryFiles
{
    public static string Find(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string[] startPaths = [Directory.GetCurrentDirectory()];
        string? path = TryFind(relativePath, startPaths);

        return path
            ?? throw new InvalidOperationException($"Repository file '{relativePath}' was not found.");
    }

    public static string? TryFindFromCurrentOrBaseDirectory(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];

        return TryFind(relativePath, startPaths);
    }

    private static string? TryFind(string relativePath, IReadOnlyList<string> startPaths)
    {
        foreach (string startPath in startPaths)
        {
            DirectoryInfo? directory = new(startPath);

            while (directory is not null)
            {
                string candidatePath = Path.Combine(directory.FullName, relativePath);

                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
