using System.Runtime.CompilerServices;

namespace AtomicArt.Desktop.Tests.Common;

internal static class DesktopTestDirectories
{
    private const string RootDirectoryName = "AtomicArtDesktopTests";

    internal static string CreateCleanDirectory(
        string name,
        [CallerFilePath] string sourceFilePath = "")
    {
        string directory = GetDirectoryPath(name, sourceFilePath);

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);

        return directory;
    }

    internal static string CreateUniqueDirectoryPath(
        [CallerFilePath] string sourceFilePath = "")
    {
        return GetDirectoryPath(Guid.NewGuid().ToString("N"), sourceFilePath);
    }

    internal static string GetDirectoryPath(
        string name,
        [CallerFilePath] string sourceFilePath = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        string sourceName = Path.GetFileNameWithoutExtension(sourceFilePath);

        return Path.Combine(
            Path.GetTempPath(),
            RootDirectoryName,
            sourceName,
            name);
    }
}
