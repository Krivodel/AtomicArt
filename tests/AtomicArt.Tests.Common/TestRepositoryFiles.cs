namespace AtomicArt.Tests.Common;

public static class TestRepositoryFiles
{
    public static string Find(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidatePath = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{relativePath}' was not found.");
    }
}
