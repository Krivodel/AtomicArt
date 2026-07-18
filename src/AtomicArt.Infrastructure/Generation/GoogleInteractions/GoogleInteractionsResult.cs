using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed record GoogleInteractionsResult(
    IReadOnlyList<GoogleInteractionImageContent> Images,
    GenerationUsageDto? Usage);
