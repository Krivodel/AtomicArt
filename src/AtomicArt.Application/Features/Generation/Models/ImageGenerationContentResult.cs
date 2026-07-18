using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record ImageGenerationContentResult(
    string ContentType,
    string Base64Data,
    GenerationUsageDto? Usage = null,
    GenerationPriceDto? Price = null,
    DateTime? CompletedAtUtc = null,
    TimeSpan? GenerationDuration = null);
