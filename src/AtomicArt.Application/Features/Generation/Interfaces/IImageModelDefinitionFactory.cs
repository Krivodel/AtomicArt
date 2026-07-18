using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelDefinitionFactory
{
    int Priority { get; }

    bool CanCreate(GenerationModelMetadataDto metadata);

    IImageModelDefinition Create(GenerationModelMetadataDto metadata);
}
