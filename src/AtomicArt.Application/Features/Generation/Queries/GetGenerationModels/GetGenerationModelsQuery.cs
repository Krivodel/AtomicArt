using MediatR;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;

public sealed record GetGenerationModelsQuery : IRequest<GenerationModelCatalogDto>;
