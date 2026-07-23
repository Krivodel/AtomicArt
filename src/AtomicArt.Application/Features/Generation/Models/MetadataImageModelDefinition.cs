using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed class MetadataImageModelDefinition : IImageModelDefinition
{
    public GenerationModelMetadataDto Metadata { get; }
    public GenerationModelConstraints Constraints => _constraints;

    private readonly GenerationModelConstraints _constraints;

    public MetadataImageModelDefinition(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadata.Attachments);
        _constraints = GenerationModelMetadataDomainMapper.ToConstraints(metadata);
        Metadata = metadata;
    }
}
