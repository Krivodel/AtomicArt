using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageGenerationWriter
{
    Task GenerateAsync(
        ImageGenerationRequestDto request,
        GenerationBatchDto batch,
        CancellationToken ct);
}
