using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryItemState : IGalleryItemStateSource
{
    public Guid Id { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ModelDisplayName { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string AspectRatio { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public GenerationItemStatus Status { get; init; }
    public GenerationItemStatus StatusKind => Status;
    public string? ImagePath { get; init; }
    public string? ThumbnailPath { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public TimeSpan? GenerationDuration { get; init; }
    public GenerationPriceDto? Price { get; init; }
    public GenerationUsageDto? Usage { get; init; }
    public int AttachedImagesCount { get; init; }
    public Guid? CorrelationId { get; init; }
    public int? GenerationOrdinal { get; init; }
}
