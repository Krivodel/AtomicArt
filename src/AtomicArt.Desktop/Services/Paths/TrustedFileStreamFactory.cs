using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.Paths;

public sealed class TrustedFileStreamFactory
{
    private readonly int _bufferSize;

    public TrustedFileStreamFactory(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _bufferSize = options.Value.TrustedFileStreamBufferSize;
    }

    public FileStream CreateNewFileForWrite(
        string trustedDirectory,
        string path,
        string failureMessage)
    {
        return TrustedPathGuard.CreateTrustedNewFileForWrite(
            trustedDirectory,
            path,
            failureMessage,
            _bufferSize);
    }

    public bool TryOpenExistingFileForRead(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        string failureMessage,
        out FileStream? stream,
        out string? trustedPath)
    {
        return TrustedPathGuard.TryOpenTrustedExistingFileForRead(
            path,
            trustedDirectories,
            trustedRootDirectory,
            failureMessage,
            _bufferSize,
            out stream,
            out trustedPath);
    }
}
