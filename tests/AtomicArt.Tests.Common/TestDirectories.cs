namespace AtomicArt.Tests.Common;

public static class TestDirectories
{
    public static void DeleteIfExists(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
