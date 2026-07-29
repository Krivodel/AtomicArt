namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelOptionMetadataDto(
    string Value,
    string? LocalizationKey = null);
