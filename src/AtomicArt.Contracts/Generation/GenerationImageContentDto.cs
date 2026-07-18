namespace AtomicArt.Contracts.Generation;

public sealed record GenerationImageContentDto(
    string ContentType,
    string Base64Data);
