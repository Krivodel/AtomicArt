using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Tests.Generation;

internal static class MetadataImageModelTestFactory
{
    public static MetadataImageModelDefinition CreateDefinition()
    {
        return MetadataImageModelTestFactory.CreateDefinition(
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata());
    }

    public static MetadataImageModelDefinition CreateDefinition(
        GenerationModelMetadataDto metadata)
    {
        IImageModelDefinition definition = MetadataImageModelTestFactory
            .CreateDefinitionFactory()
            .Create(metadata);

        return (MetadataImageModelDefinition)definition;
    }

    public static ImageModelRegistry CreateRegistry()
    {
        return MetadataImageModelTestFactory.CreateRegistry(
            ApiModelMetadataTestCatalog.LoadCatalog());
    }

    public static ImageModelRegistry CreateRegistry(
        GenerationModelMetadataDto metadata)
    {
        List<GenerationModelMetadataDto> models = [metadata];
        GenerationModelCatalogDto catalog = new(models);

        return MetadataImageModelTestFactory.CreateRegistry(catalog);
    }

    public static ImageModelRegistry CreateRegistry(GenerationModelCatalogDto catalog)
    {
        IImageModelDefinitionFactory[] factories =
        [
            MetadataImageModelTestFactory.CreateDefinitionFactory()
        ];

        return new ImageModelRegistry(catalog, factories);
    }

    private static MetadataImageModelDefinitionFactory CreateDefinitionFactory()
    {
        IGenerationModelRules[] modelRules = [new MetadataGenerationModelRules()];
        GenerationModelRules rules = new(modelRules);
        IReadOnlyList<IAttachedImageFormat> formats = GenerationImageFileFormats.All
            .Select<GenerationImageFileFormatDescriptor, IAttachedImageFormat>(
                format => new AttachedImageFormat(format))
            .ToList();

        return new MetadataImageModelDefinitionFactory(rules, formats);
    }
}
