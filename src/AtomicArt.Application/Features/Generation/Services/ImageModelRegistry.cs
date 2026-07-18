using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Common;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public sealed class ImageModelRegistry : IImageModelRegistry
{
    private readonly IReadOnlyList<IImageModelDefinition> _definitions;
    private readonly Dictionary<string, IImageModelDefinition> _definitionsById;

    public ImageModelRegistry(
        GenerationModelCatalogDto catalog,
        IEnumerable<IImageModelDefinitionFactory> factories)
        : this(CreateDefinitions(catalog, factories))
    {
    }

    private ImageModelRegistry(IReadOnlyList<IImageModelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _definitions = definitions;
        _definitionsById = new Dictionary<string, IImageModelDefinition>(StringComparer.Ordinal);

        foreach (IImageModelDefinition definition in definitions)
        {
            string modelId = definition.Constraints.ModelId;

            if (_definitionsById.ContainsKey(modelId))
            {
                throw new InvalidOperationException($"Generation model '{modelId}' is registered more than once.");
            }

            _definitionsById.Add(modelId, definition);
        }
    }

    public IReadOnlyList<GenerationModelMetadataDto> GetModels()
    {
        List<GenerationModelMetadataDto> models = _definitions
            .Select(CreateMetadata)
            .ToList();

        return models;
    }

    public IImageModelDefinition? GetById(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (_definitionsById.TryGetValue(modelId, out IImageModelDefinition? definition))
        {
            return definition;
        }

        return null;
    }

    private static GenerationModelMetadataDto CreateMetadata(IImageModelDefinition definition)
    {
        GenerationModelConstraints constraints = definition.Constraints;

        return new GenerationModelMetadataDto(
            constraints.ModelId,
            definition.DisplayName,
            definition.Provider,
            definition.ProviderModelId,
            definition.PanelId,
            definition.ContextWindowTokens,
            definition.MaxOutputTokens,
            constraints.MaxPromptLength,
            constraints.AspectRatios,
            constraints.Resolutions,
            constraints.GenerationCounts,
            definition.Temperature,
            new GenerationModelAttachmentMetadataDto(
                constraints.MaxAttachedImages,
                constraints.MaxAttachedImageBytes,
                constraints.MaxTotalAttachedImageBytes,
                constraints.SupportedContentTypes),
            definition.Pricing,
            definition.Thinking);
    }

    private static IReadOnlyList<IImageModelDefinition> CreateDefinitions(
        GenerationModelCatalogDto catalog,
        IEnumerable<IImageModelDefinitionFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factories);

        if (catalog.Models is null || catalog.Models.Count == 0)
        {
            throw new InvalidOperationException(
                "The generation model catalog is empty.");
        }

        IReadOnlyList<IImageModelDefinitionFactory> factoryList = CreateFactorySnapshot(factories);

        List<IImageModelDefinition> definitions = [];

        foreach (GenerationModelMetadataDto metadata in catalog.Models)
        {
            definitions.Add(CreateDefinition(metadata, factoryList));
        }

        return definitions;
    }

    private static IReadOnlyList<IImageModelDefinitionFactory> CreateFactorySnapshot(
        IEnumerable<IImageModelDefinitionFactory> factories)
    {
        List<IImageModelDefinitionFactory> factoryList = factories.ToList();

        if (factoryList.Count == 0)
        {
            throw new InvalidOperationException(
                "No generation model factories are registered.");
        }

        if (factoryList.Any(factory => factory is null))
        {
            throw new InvalidOperationException(
                "A null generation model factory is registered.");
        }

        return factoryList;
    }

    private static IImageModelDefinition CreateDefinition(
        GenerationModelMetadataDto metadata,
        IReadOnlyList<IImageModelDefinitionFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Id);

        IImageModelDefinitionFactory selectedFactory = UniqueHighestPrioritySelector.Select(
            factories,
            factory => factory.CanCreate(metadata),
            factory => factory.Priority,
            () => new InvalidOperationException(
                $"No generation model factory is registered for model '{metadata.Id}'."),
            priority => new InvalidOperationException(
                $"Multiple generation model factories with priority {priority} are registered for model '{metadata.Id}'."));

        return selectedFactory.Create(metadata);
    }
}
