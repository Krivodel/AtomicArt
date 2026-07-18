namespace Pica.Viewer.Tests;

internal sealed class PicaTemporaryDirectory : IDisposable
{
    private const string RootDirectoryName = "Pica.Viewer.Tests";

    public string DirectoryPath { get; }

    private bool _disposed;

    private PicaTemporaryDirectory(string directoryPath)
    {
        DirectoryPath = directoryPath;
        Directory.CreateDirectory(DirectoryPath);
    }

    public static PicaTemporaryDirectory Create()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            RootDirectoryName,
            Guid.NewGuid().ToString("N"));

        return new PicaTemporaryDirectory(directoryPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}
