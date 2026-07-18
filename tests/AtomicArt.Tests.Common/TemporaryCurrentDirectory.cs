namespace AtomicArt.Tests.Common;

public sealed class TemporaryCurrentDirectory : IDisposable
{
    public string DirectoryPath { get; }

    private readonly string _previousDirectory;
    private bool _disposed;

    public TemporaryCurrentDirectory(Type testType, string testName)
    {
        ArgumentNullException.ThrowIfNull(testType);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        string rootDirectoryName = testType.Assembly.GetName().Name
            ?? throw new InvalidOperationException("Test assembly name is missing.");
        _previousDirectory = Directory.GetCurrentDirectory();
        DirectoryPath = Path.Combine(Path.GetTempPath(), rootDirectoryName, testName);

        TestDirectories.DeleteIfExists(DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);

        try
        {
            Directory.SetCurrentDirectory(DirectoryPath);
        }
        catch
        {
            TestDirectories.DeleteIfExists(DirectoryPath);
            throw;
        }
    }

    public IReadOnlyList<string> GetEntries()
    {
        return Directory
            .EnumerateFileSystemEntries(DirectoryPath, "*", SearchOption.AllDirectories)
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Directory.SetCurrentDirectory(_previousDirectory);
        TestDirectories.DeleteIfExists(DirectoryPath);
        _disposed = true;
    }
}
