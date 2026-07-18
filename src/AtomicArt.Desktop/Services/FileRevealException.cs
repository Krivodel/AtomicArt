namespace AtomicArt.Desktop.Services;

public sealed class FileRevealException : InvalidOperationException
{
    public FileRevealException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
