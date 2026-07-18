using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageGenerationOutputPlanner
{
    ImageGenerationOutputPlan CreatePlan(
        ImageGenerationRequestDto request,
        Guid batchId,
        string modelDisplayName);
}
