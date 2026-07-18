using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

public sealed class FakeImageGenerationOutputPlanner : IImageGenerationOutputPlanner
{
    public ImageGenerationOutputPlan CreatePlan(
        ImageGenerationRequestDto request,
        Guid batchId,
        string modelDisplayName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(batchId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDisplayName);

        DateTime createdAtUtc = DateTime.UtcNow;
        IReadOnlyList<ImageGenerationOutputItemPlan> items = Enumerable
            .Range(0, request.GenerationCount)
            .Select(_ => CreateItemPlan(batchId, createdAtUtc))
            .ToList();

        return new ImageGenerationOutputPlan(items);
    }

    private static ImageGenerationOutputItemPlan CreateItemPlan(
        Guid batchId,
        DateTime createdAtUtc)
    {
        Guid itemId = Guid.NewGuid();

        return new ImageGenerationOutputItemPlan(
            itemId,
            createdAtUtc);
    }
}
