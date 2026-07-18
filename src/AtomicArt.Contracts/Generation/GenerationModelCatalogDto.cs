namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelCatalogDto(
    IReadOnlyList<GenerationModelMetadataDto> Models);
