using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationTemporaryResult : IAsyncDisposable
{
    public Stream Stream => _stream;
    public string? FinalPath { get; private set; }

    private readonly string _temporaryPath;
    private readonly string _resultsDirectory;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private FileStream _stream;
    private bool _committed;
    private bool _disposed;

    internal GenerationTemporaryResult(
        string temporaryPath,
        FileStream stream,
        string resultsDirectory,
        IGenerationImageFormatRegistry formatRegistry,
        GenerationImageFileNamePolicy fileNamePolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultsDirectory);

        _temporaryPath = temporaryPath;
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _resultsDirectory = resultsDirectory;
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _fileNamePolicy = fileNamePolicy
            ?? throw new ArgumentNullException(nameof(fileNamePolicy));
    }

    public async Task CommitAsync(
        Guid batchId,
        Guid itemId,
        string contentType,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (!_formatRegistry.TryGetByContentType(
                contentType,
                out IGenerationImageFormat? format)
            || format is null)
        {
            throw new InvalidDataException(
                "Generation response declared an unsupported image type.");
        }

        await _stream.FlushAsync(ct).ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
        await ValidateSignatureAsync(format, ct).ConfigureAwait(false);

        string fileName = _fileNamePolicy.BuildFileName(
            batchId,
            itemId,
            format.Extension);
        string finalPath = Path.GetFullPath(
            Path.Combine(_resultsDirectory, fileName));

        if (!TrustedPathGuard.IsInsideDirectory(_resultsDirectory, finalPath))
        {
            throw new InvalidDataException(
                "Generation result path is outside the trusted directory.");
        }

        File.Move(_temporaryPath, finalPath);
        FinalPath = finalPath;
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);

        if (!_committed)
        {
            File.Delete(_temporaryPath);
        }
    }

    private async Task ValidateSignatureAsync(
        IGenerationImageFormat format,
        CancellationToken ct)
    {
        const int SignatureProbeLength = 64;

        byte[] probe = new byte[SignatureProbeLength];
        await using FileStream input = new(
            _temporaryPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            SignatureProbeLength,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        int bytesRead = await input
            .ReadAsync(probe, ct)
            .ConfigureAwait(false);

        if (!format.MatchesSignature(probe.AsSpan(0, bytesRead)))
        {
            throw new InvalidDataException(
                "Generation image signature does not match its content type.");
        }
    }
}
