using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryItemStateSource
{
    Guid Id { get; }
    string ModelId { get; }
    string ModelDisplayName { get; }
    string Prompt { get; }
    string AspectRatio { get; }
    string Resolution { get; }
    GenerationRequestSettings? RequestSettings { get; }
    DateTime CreatedAtUtc { get; }
    DateTime? GalleryOrderTimestampUtc { get; }
    GenerationItemStatus StatusKind { get; }
    string? ImagePath { get; }
    string? ThumbnailPath { get; }
    DateTime? CompletedAtUtc { get; }
    TimeSpan? GenerationDuration { get; }
    GenerationPriceDto? Price { get; }
    GenerationUsageDto? Usage { get; }
    string? FailureCode { get; }
    int AttachedImagesCount { get; }
    bool IsFavorite { get; }
    Guid? CorrelationId { get; }
    int? GenerationOrdinal { get; }
}
