namespace AtomicArt.Tests.Common;

public static class TestDirectories
{
    public static string GetUniqueDirectoryPath(Type testType)
    {
        ArgumentNullException.ThrowIfNull(testType);

        return Path.Combine(
            Path.GetTempPath(),
            $"AtomicArt.{testType.Name}",
            Guid.NewGuid().ToString("N"));
    }

    public static void DeleteIfExists(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
