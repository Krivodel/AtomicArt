namespace AtomicArt.Contracts.Generation;

public sealed record AttachedImageDto(
    string FileName,
    string ContentType,
    byte[] Content);
