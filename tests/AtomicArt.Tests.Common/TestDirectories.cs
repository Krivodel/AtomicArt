namespace AtomicArt.Tests.Common;

public static class TestDirectories
{
    public static string GetAssemblyTestDirectoryPath(Type testType, string testName)
    {
        ArgumentNullException.ThrowIfNull(testType);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        return Path.Combine(
            Path.GetTempPath(),
            GetAssemblyName(testType),
            testName);
    }

    public static string GetUniqueDirectoryPath(Type testType)
    {
        ArgumentNullException.ThrowIfNull(testType);

        return Path.Combine(
            Path.GetTempPath(),
            $"AtomicArt.{testType.Name}",
            Guid.NewGuid().ToString("N"));
    }

    public static string GetUniqueDirectoryPath(Type testType, string testName)
    {
        ArgumentNullException.ThrowIfNull(testType);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        return Path.Combine(
            Path.GetTempPath(),
            GetAssemblyName(testType),
            testType.Name,
            testName,
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

    private static string GetAssemblyName(Type testType)
    {
        return testType.Assembly.GetName().Name
            ?? throw new InvalidOperationException("Test assembly name is missing.");
    }
}
