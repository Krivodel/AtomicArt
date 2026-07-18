namespace AtomicArt.Application.Features.Generation.Models;

public sealed record ImageGenerationOutputPlan(
    IReadOnlyList<ImageGenerationOutputItemPlan> Items);
