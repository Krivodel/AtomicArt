namespace AtomicArt.Domain.Generation;

public sealed record GenerationAttachedImage(
    string? ContentType,
    long SizeInBytes);
