using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Generation;

internal sealed class TemporaryGenerationAttachmentSource
    : IGenerationAttachmentSource, IAsyncDisposable
{
    public GenerationAttachmentMetadataDto Metadata { get; }

    private readonly string _path;
    private int _disposed;

    public TemporaryGenerationAttachmentSource(
        GenerationAttachmentMetadataDto metadata,
        string path)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    public ValueTask<Stream> OpenReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        Stream stream = new FileStream(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return ValueTask.FromResult(stream);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            File.Delete(_path);
        }

        return ValueTask.CompletedTask;
    }
}
