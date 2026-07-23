namespace AtomicArt.Contracts.Generation;

public sealed record GenerationAttemptMetadataDto(
    Guid LogicalGenerationId,
    int AttemptNumber,
    string ProviderId,
    Guid BatchId,
    Guid ItemId,
    string ModelDisplayName,
    GenerationItemStatus Status,
    string? ProviderState,
    int ResultCount,
    IReadOnlyList<string> ContentTypes,
    GenerationUsageDto? Usage,
    GenerationPriceDto? Price,
    DateTime CompletedAtUtc,
    TimeSpan GenerationDuration,
    string? SafeErrorCode,
    bool Retryable);
