namespace AtomicArt.Contracts.Generation;

public sealed record GenerationImageFileSignaturePart(
    int Offset,
    IReadOnlyList<byte> Bytes);
