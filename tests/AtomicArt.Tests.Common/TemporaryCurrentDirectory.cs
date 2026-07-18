namespace AtomicArt.Tests.Common;

public sealed class TemporaryCurrentDirectory : IDisposable
{
    public string DirectoryPath => _temporaryDirectory.DirectoryPath;

    private readonly TemporaryDirectory _temporaryDirectory;
    private readonly string _previousDirectory;
    private bool _disposed;

    public TemporaryCurrentDirectory(Type testType, string testName)
    {
        _previousDirectory = Directory.GetCurrentDirectory();
        string directoryPath = TestDirectories.GetAssemblyTestDirectoryPath(testType, testName);
        _temporaryDirectory = new TemporaryDirectory(directoryPath);

        try
        {
            Directory.SetCurrentDirectory(DirectoryPath);
        }
        catch
        {
            _temporaryDirectory.Dispose();
            throw;
        }
    }

    public IReadOnlyList<string> GetEntries()
    {
        return _temporaryDirectory.GetEntries();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Directory.SetCurrentDirectory(_previousDirectory);
        _temporaryDirectory.Dispose();
        _disposed = true;
    }
}
