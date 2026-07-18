using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MediatR;

using AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Controllers;

[ApiController]
[Route(GenerationApiRoutes.Models)]
public sealed class GenerationModelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GenerationModelsController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);

        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GenerationModelCatalogDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        GenerationModelCatalogDto catalog = await _mediator
            .Send(new GetGenerationModelsQuery(), ct)
            .ConfigureAwait(false);

        return Ok(catalog);
    }
}
