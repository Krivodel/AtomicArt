using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record ProviderGenerationSummary(
    string? State,
    int ResultCount,
    IReadOnlyList<string> ContentTypes,
    GenerationUsageDto? Usage);
