using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed class MetadataImageModelDefinitionFactory : IImageModelDefinitionFactory
{
    public int Priority => 0;

    public bool CanCreate(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return true;
    }

    public IImageModelDefinition Create(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new MetadataImageModelDefinition(metadata);
    }
}
