namespace AtomicArt.Contracts.Generation;

public sealed record GenerationAttachmentMetadataDto(
    int Index,
    string FileName,
    string ContentType,
    long ByteLength,
    int PixelWidth = 0,
    int PixelHeight = 0);
