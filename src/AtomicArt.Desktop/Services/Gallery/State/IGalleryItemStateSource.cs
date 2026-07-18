using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryItemStateSource
{
    Guid Id { get; }
    string ModelId { get; }
    string ModelDisplayName { get; }
    string Prompt { get; }
    string AspectRatio { get; }
    string Resolution { get; }
    DateTime CreatedAtUtc { get; }
    GenerationItemStatus StatusKind { get; }
    string? ImagePath { get; }
    string? ThumbnailPath { get; }
    DateTime? CompletedAtUtc { get; }
    TimeSpan? GenerationDuration { get; }
    GenerationPriceDto? Price { get; }
    GenerationUsageDto? Usage { get; }
    int AttachedImagesCount { get; }
    Guid? CorrelationId { get; }
    int? GenerationOrdinal { get; }
}
