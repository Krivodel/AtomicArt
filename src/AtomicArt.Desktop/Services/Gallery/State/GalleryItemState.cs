using System.Text.Json.Serialization;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryItemState : IGalleryItemStateSource
{
    public Guid Id { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ModelDisplayName { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string AspectRatio { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GenerationRequestSettings? RequestSettings { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? GalleryOrderTimestampUtc { get; init; }
    public GenerationItemStatus Status { get; init; }
    public GenerationItemStatus StatusKind => Status;
    public string? ImagePath { get; init; }
    public string? ThumbnailPath { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public TimeSpan? GenerationDuration { get; init; }
    public GenerationPriceDto? Price { get; init; }
    public GenerationUsageDto? Usage { get; init; }
    public string? FailureCode { get; init; }
    public int AttachedImagesCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<GenerationReference>? References { get; init; }
    public bool IsFavorite { get; init; }
    public Guid? CorrelationId { get; init; }
    public int? GenerationOrdinal { get; init; }
}
