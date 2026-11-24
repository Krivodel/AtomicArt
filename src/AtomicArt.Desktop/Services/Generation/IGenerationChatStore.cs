namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationChatStore
{
    Task<GenerationChatHistory> LoadAsync(Guid id, CancellationToken ct);

    Task SaveAsync(Guid id, GenerationChatHistory history, CancellationToken ct);

    Task PruneAsync(IReadOnlySet<Guid> retainedIds, CancellationToken ct);
}
