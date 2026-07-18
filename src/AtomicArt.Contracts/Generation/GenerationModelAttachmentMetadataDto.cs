namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelAttachmentMetadataDto(
    int MaxCount,
    long MaxSingleFileBytes,
    long MaxTotalBytes,
    IReadOnlyList<string> SupportedContentTypes);
