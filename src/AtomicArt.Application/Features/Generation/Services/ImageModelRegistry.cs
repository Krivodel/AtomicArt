using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

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
            if (_definitionsById.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException($"Generation model '{definition.Id}' is registered more than once.");
            }

            _definitionsById.Add(definition.Id, definition);
        }
    }

    public IReadOnlyList<ImageModelOption> GetModels()
    {
        List<ImageModelOption> models = _definitions
            .Select(definition => new ImageModelOption(
                definition.Id,
                definition.DisplayName,
                definition.Provider,
                definition.ProviderModelId,
                definition.PanelId,
                definition.ContextWindowTokens,
                definition.MaxOutputTokens,
                definition.GetAspectRatios(),
                definition.GetResolutions(),
                definition.GetGenerationCounts(),
                definition.Temperature,
                definition.MaxAttachedImages,
                definition.MaxPromptLength,
                definition.MaxAttachedImageBytes,
                definition.MaxTotalAttachedImageBytes,
                definition.GetSupportedContentTypes(),
                definition.Pricing,
                definition.Thinking))
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

        List<IImageModelDefinitionFactory> matchingFactories = factories
            .Where(factory => factory.CanCreate(metadata))
            .OrderByDescending(factory => factory.Priority)
            .ToList();

        if (matchingFactories.Count == 0)
        {
            throw new InvalidOperationException(
                $"No generation model factory is registered for model '{metadata.Id}'.");
        }

        IImageModelDefinitionFactory selectedFactory = matchingFactories[0];

        if (matchingFactories.Count > 1 && matchingFactories[1].Priority == selectedFactory.Priority)
        {
            throw new InvalidOperationException(
                $"Multiple generation model factories with priority {selectedFactory.Priority} are registered for model '{metadata.Id}'.");
        }

        return selectedFactory.Create(metadata);
    }
}
