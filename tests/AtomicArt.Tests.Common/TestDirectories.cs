namespace AtomicArt.Tests.Common;

public static class TestDirectories
{
    public static string GetAssemblyTestDirectoryPath(Type testType, string testName)
    {
        ValidateAssemblyTestPathArguments(testType, testName);

        return Path.Combine(GetAssemblyDirectoryPath(testType), testName);
    }

    public static string GetUniqueDirectoryPath(Type testType)
    {
        ValidateTestType(testType);

        return Path.Combine(
            Path.GetTempPath(),
            $"AtomicArt.{testType.Name}",
            Guid.NewGuid().ToString("N"));
    }

    public static string GetUniqueDirectoryPath(Type testType, string testName)
    {
        ValidateAssemblyTestPathArguments(testType, testName);

        return Path.Combine(
            GetAssemblyDirectoryPath(testType),
            testType.Name,
            testName,
            Guid.NewGuid().ToString("N"));
    }

    public static string GetUniqueAssemblyDirectoryPath(Type testType)
    {
        ValidateTestType(testType);

        return Path.Combine(
            GetAssemblyDirectoryPath(testType),
            testType.Name,
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

    private static string GetAssemblyDirectoryPath(Type testType)
    {
        return Path.Combine(
            Path.GetTempPath(),
            GetAssemblyName(testType));
    }

    private static string GetAssemblyName(Type testType)
    {
        return testType.Assembly.GetName().Name
            ?? throw new InvalidOperationException("Test assembly name is missing.");
    }

    private static void ValidateAssemblyTestPathArguments(Type testType, string testName)
    {
        ValidateTestType(testType);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
    }

    private static void ValidateTestType(Type testType)
    {
        ArgumentNullException.ThrowIfNull(testType);
    }
}
