namespace AtomicArt.Tests.Common;

public sealed class TemporaryDirectory : IDisposable
{
    public string DirectoryPath { get; }

    private bool _disposed;

    public TemporaryDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        DirectoryPath = directoryPath;
        TestDirectories.DeleteIfExists(DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public TemporaryDirectory(Type testType, string testName)
        : this(TestDirectories.GetUniqueDirectoryPath(testType, testName))
    {
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

        TestDirectories.DeleteIfExists(DirectoryPath);
        _disposed = true;
    }
}
