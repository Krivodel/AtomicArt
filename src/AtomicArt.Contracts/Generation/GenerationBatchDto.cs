namespace AtomicArt.Contracts.Generation;

public sealed record GenerationBatchDto(
    Guid BatchId,
    IReadOnlyList<GenerationItemDto> Items);
