using MediatR;

using AtomicArt.Application.Features.Generation.Interfaces;
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
        List<GenerationModelMetadataDto> metadata = _registry
            .GetModels()
            .ToList();
        GenerationModelCatalogDto catalog = new(metadata.AsReadOnly());

        return Task.FromResult(catalog);
    }
}
