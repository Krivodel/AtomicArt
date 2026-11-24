using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationReferenceStore
{
    Task<IReadOnlyList<GenerationReference>> SaveAsync(
        Guid runId,
        IReadOnlyList<AttachedImageDto> images,
        CancellationToken ct);

    Task<AttachedImageDto?> LoadAsync(GenerationReference reference, CancellationToken ct);

    Task SaveOwnedAsync(string relativeOwnerPath, AttachedImageDto image, CancellationToken ct);

    string? ResolveExistingFilePath(GenerationReference reference);

    Task<string?> GetExistingFilePathAsync(GenerationReference reference, CancellationToken ct);

    Task<TResult?> ReadAsync<TResult>(
        GenerationReference reference,
        Func<Stream, CancellationToken, Task<TResult?>> read,
        CancellationToken ct) where TResult : class;

    void Release(Guid runId);

    Task RetainAsync(Guid runId, IEnumerable<GenerationReference> references, CancellationToken ct);

    Task RetainReadPathsAsync(Guid runId, IEnumerable<GenerationReference> references, CancellationToken ct);

    Task PromoteAsync(Guid runId, IReadOnlyList<GenerationReference> references, CancellationToken ct);

    Task SynchronizeAsync(IReadOnlySet<string> galleryHashes, IReadOnlySet<string> retainedHashes, CancellationToken ct);

    Task PruneAsync(IReadOnlySet<string> retainedHashes, CancellationToken ct);
}
