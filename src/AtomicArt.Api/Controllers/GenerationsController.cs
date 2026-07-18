using Microsoft.AspNetCore.Mvc;

using MediatR;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Controllers;

[ApiController]
[Route(GenerationApiRoutes.Generations)]
public sealed class GenerationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly GenerationModelCatalogDto _modelCatalog;
    private readonly ILogger<GenerationsController> _logger;

    public GenerationsController(
        IMediator mediator,
        GenerationModelCatalogDto modelCatalog,
        ILogger<GenerationsController> logger)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(logger);

        _mediator = mediator;
        _modelCatalog = modelCatalog;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(GenerationBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ImageGenerationRequestDto request,
        CancellationToken ct)
    {
        string? providerCredential = Request.Headers[GenerationApiRoutes.ProviderApiKeyHeaderName]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providerCredential))
        {
            if (RequiresProviderCredential(request.ModelId))
            {
                _logger.LogWarning(
                    "Generation request was rejected at the API boundary because required provider credentials are missing.");

                ProblemDetails problemDetails = CreateProblemDetails(
                    StatusCodes.Status401Unauthorized,
                    "Provider credential не передан.",
                    "Для выбранной модели требуется ключ провайдера.",
                    null);

                return StatusCode(StatusCodes.Status401Unauthorized, problemDetails);
            }
        }

        CreateImageGenerationCommand command = new(request, providerCredential);
        Result<GenerationBatchDto> result = await _mediator
            .Send(command, ct)
            .ConfigureAwait(false);

        return CreateResponse(
            result,
            "Ошибка запроса генерации.",
            GetCreateFailureStatusCode);
    }

    private IActionResult CreateResponse<TResponse>(
        Result<TResponse> result,
        string problemTitle,
        Func<Result<TResponse>, int> getFailureStatusCode)
        where TResponse : class
    {
        if (result is { IsSuccess: true, Value: { } value })
        {
            return Ok(value);
        }

        int statusCode = getFailureStatusCode(result);
        ProblemDetails problemDetails = CreateProblemDetails(
            statusCode,
            problemTitle,
            "Запрос не прошёл проверку.",
            result.ErrorCode);

        return StatusCode(statusCode, problemDetails);
    }

    private static int GetCreateFailureStatusCode<TResponse>(Result<TResponse> result)
    {
        if (result.IsUnavailable)
        {
            return GetCreateUnavailableStatusCode(result.ErrorCode);
        }

        return result.Status switch
        {
            ResultStatus.ValidationError or ResultStatus.NotFound => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static int GetCreateUnavailableStatusCode(string? errorCode)
    {
        return errorCode switch
        {
            ImageGenerationProviderErrorCodes.Authentication => StatusCodes.Status401Unauthorized,
            ImageGenerationProviderErrorCodes.Authorization => StatusCodes.Status403Forbidden,
            ImageGenerationProviderErrorCodes.RateLimited => StatusCodes.Status429TooManyRequests,
            ImageGenerationProviderErrorCodes.RequestRejected
                or ImageGenerationProviderErrorCodes.ResourceNotFound
                or ImageGenerationProviderErrorCodes.InternalError
                or ImageGenerationProviderErrorCodes.Unknown
                => StatusCodes.Status502BadGateway,
            ImageGenerationProviderErrorCodes.InvalidResponse => StatusCodes.Status502BadGateway,
            ImageGenerationProviderErrorCodes.Timeout => StatusCodes.Status504GatewayTimeout,
            ImageGenerationProviderErrorCodes.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private bool RequiresProviderCredential(string modelId)
    {
        GenerationModelMetadataDto? model = _modelCatalog.Models?
            .FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));

        return model is not null
            && GenerationProviderCredentialRequirements
                .Resolve(model.Provider)
                .RequiredAtApiBoundary;
    }

    private static ProblemDetails CreateProblemDetails(
        int statusCode,
        string title,
        string detail,
        string? errorCode)
    {
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName] = errorCode;
        }

        return problemDetails;
    }
}
