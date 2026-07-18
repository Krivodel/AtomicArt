namespace AtomicArt.Contracts.Generation;

public sealed record GenerationImageFileFormatDescriptor(
    string ContentType,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<IReadOnlyList<GenerationImageFileSignaturePart>> SignatureAlternatives);
