using MediatR;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;

public sealed class GetGenerationModelsHandler
    : IRequestHandler<GetGenerationModelsQuery, GenerationModelCatalogDto>
{
    private readonly IImageModelRegistry _registry;

    public GetGenerationModelsHandler(IImageModelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public Task<GenerationModelCatalogDto> Handle(
        GetGenerationModelsQuery request,
        CancellationToken ct)
    {
        IReadOnlyList<ImageModelOption> models = _registry.GetModels();
        List<GenerationModelMetadataDto> metadata = models
            .Select(CreateMetadata)
            .ToList();
        GenerationModelCatalogDto catalog = new(metadata.AsReadOnly());

        return Task.FromResult(catalog);
    }

    private static GenerationModelMetadataDto CreateMetadata(ImageModelOption model)
    {
        return new GenerationModelMetadataDto(
            model.Id,
            model.DisplayName,
            model.Provider,
            model.ProviderModelId,
            model.PanelId,
            model.ContextWindowTokens,
            model.MaxOutputTokens,
            GetRequiredMaxPromptLength(model),
            model.AspectRatios,
            model.Resolutions,
            model.GenerationCounts,
            model.Temperature,
            new GenerationModelAttachmentMetadataDto(
                model.MaxAttachedImages,
                model.MaxAttachedImageBytes,
                model.MaxTotalAttachedImageBytes,
                model.SupportedContentTypes),
            model.Pricing,
            model.Thinking);
    }

    private static int GetRequiredMaxPromptLength(ImageModelOption model)
    {
        if (model.MaxPromptLength is { } maxPromptLength)
        {
            return maxPromptLength;
        }

        throw new InvalidOperationException(
            $"Generation model '{model.Id}' does not define the required maxPromptLength value.");
    }
}
