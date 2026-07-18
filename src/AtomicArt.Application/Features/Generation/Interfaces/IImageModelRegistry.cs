using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelRegistry
{
    IReadOnlyList<GenerationModelMetadataDto> GetModels();

    IImageModelDefinition? GetById(string modelId);
}
