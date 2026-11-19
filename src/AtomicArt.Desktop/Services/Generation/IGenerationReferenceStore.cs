using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationReferenceStore
{
    Task<IReadOnlyList<GenerationReference>> SaveAsync(
        Guid runId,
        IReadOnlyList<AttachedImageDto> images,
        CancellationToken ct);

    Task<AttachedImageDto?> LoadAsync(GenerationReference reference, CancellationToken ct);

    Task<string?> GetExistingFilePathAsync(GenerationReference reference, CancellationToken ct);

    Task<TResult?> ReadAsync<TResult>(
        GenerationReference reference,
        Func<Stream, CancellationToken, Task<TResult?>> read,
        CancellationToken ct) where TResult : class;

    void Release(Guid runId);

    Task PruneAsync(IReadOnlySet<string> retainedHashes, CancellationToken ct);
}
