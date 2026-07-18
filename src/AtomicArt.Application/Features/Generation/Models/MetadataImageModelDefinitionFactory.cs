using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed class MetadataImageModelDefinitionFactory : IImageModelDefinitionFactory
{
    public int Priority => 0;

    private readonly GenerationModelRules _rules;
    private readonly IAttachedImageFormatRegistry _formatRegistry;

    public MetadataImageModelDefinitionFactory(
        GenerationModelRules rules,
        IAttachedImageFormatRegistry formatRegistry)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(formatRegistry);

        _rules = rules;
        _formatRegistry = formatRegistry;
    }

    public bool CanCreate(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return true;
    }

    public IImageModelDefinition Create(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new MetadataImageModelDefinition(metadata, _rules, _formatRegistry);
    }
}
