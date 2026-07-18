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

        DeleteDirectoryIfExists(directory);

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

    private static void DeleteDirectoryIfExists(string directory)
    {
        DirectoryInfo directoryInfo = new(directory);

        if (!directoryInfo.Exists)
        {
            return;
        }

        DeleteDirectory(directoryInfo);
    }

    private static void DeleteDirectory(DirectoryInfo directory)
    {
        directory.Refresh();

        if (!directory.Exists)
        {
            return;
        }

        if (IsReparsePoint(directory))
        {
            directory.Delete();

            return;
        }

        foreach (FileSystemInfo child in directory.EnumerateFileSystemInfos())
        {
            if (IsReparsePoint(child))
            {
                child.Delete();

                continue;
            }

            if (child is DirectoryInfo childDirectory)
            {
                DeleteDirectory(childDirectory);

                continue;
            }

            child.Delete();
        }

        directory.Delete();
    }

    private static bool IsReparsePoint(FileSystemInfo fileSystemInfo)
    {
        return (fileSystemInfo.Attributes & FileAttributes.ReparsePoint)
            == FileAttributes.ReparsePoint;
    }
}
