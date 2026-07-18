namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelThinkingMetadataDto(
    IReadOnlyList<GenerationModelThinkingLevelMetadataDto> Levels,
    string Default);
