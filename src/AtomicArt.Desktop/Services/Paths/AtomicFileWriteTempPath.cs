namespace AtomicArt.Desktop.Services.Paths;

internal static class AtomicFileWriteTempPath
{
    private const string Extension = ".tmp";

    internal static string CreateHidden(string directory, string fileNameStem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameStem);

        return Create(directory, string.Concat(".", fileNameStem));
    }

    internal static string CreateSibling(string directory, string targetFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFileName);

        return Create(directory, targetFileName);
    }

    private static string Create(string directory, string fileNamePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string tempFileName = string.Concat(
            fileNamePrefix,
            ".",
            Guid.NewGuid().ToString("N"),
            Extension);

        return Path.Combine(directory, tempFileName);
    }
}
