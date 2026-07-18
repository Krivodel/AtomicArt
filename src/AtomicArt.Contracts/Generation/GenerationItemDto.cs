namespace AtomicArt.Contracts.Generation;

public sealed record GenerationItemDto(
    Guid Id,
    string ModelId,
    string ModelDisplayName,
    string Prompt,
    string AspectRatio,
    string Resolution,
    DateTime CreatedAtUtc,
    GenerationItemStatus Status,
    string? ImagePath,
    GenerationImageContentDto? ImageContent = null,
    DateTime? CompletedAtUtc = null,
    TimeSpan? GenerationDuration = null,
    GenerationPriceDto? Price = null,
    GenerationUsageDto? Usage = null);
