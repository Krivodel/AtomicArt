using Microsoft.AspNetCore.Mvc;

using MediatR;

using AtomicArt.Api.Generation;
using AtomicArt.Api.Filters;
using AtomicArt.Application.Features.Generation.Commands.CreateStreamingGeneration;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Controllers;

[ApiController]
[Route(GenerationApiRoutes.Generations)]
public sealed class GenerationsController : ControllerBase
{
    private const long GlobalMaximumRequestBytes = 1024L * 1024L * 1024L;

    private readonly IMediator _mediator;
    private readonly IGenerationRequestConcurrencyLimiter _concurrencyLimiter;
    private readonly MultipartGenerationRequestReader _requestReader;
    private readonly GenerationStreamingResponseWriter _responseWriter;
    private readonly ILogger<GenerationsController> _logger;

    public GenerationsController(
        IMediator mediator,
        IGenerationRequestConcurrencyLimiter concurrencyLimiter,
        MultipartGenerationRequestReader requestReader,
        GenerationStreamingResponseWriter responseWriter,
        ILogger<GenerationsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _concurrencyLimiter = concurrencyLimiter
            ?? throw new ArgumentNullException(nameof(concurrencyLimiter));
        _requestReader = requestReader
            ?? throw new ArgumentNullException(nameof(requestReader));
        _responseWriter = responseWriter
            ?? throw new ArgumentNullException(nameof(responseWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [RequestSizeLimit(GlobalMaximumRequestBytes)]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAsync(CancellationToken ct)
    {
        IDisposable? concurrencyLease = _concurrencyLimiter.TryAcquire();

        if (concurrencyLease is null)
        {
            return _responseWriter.CreateProblemResponse(
                StatusCodes.Status429TooManyRequests,
                GenerationProtocolErrorCodes.ConcurrencyLimitReached,
                null,
                false,
                null,
                null);
        }

        using (concurrencyLease)
        {
            try
            {
                await using MultipartGenerationRequest request =
                    await _requestReader.ReadAsync(Request, ct)
                        .ConfigureAwait(false);
                string? providerCredential =
                    Request.Headers[GenerationApiRoutes.ProviderApiKeyHeaderName]
                        .FirstOrDefault();
                CreateStreamingGenerationCommand command = new(
                    request.Metadata,
                    request.Attachments,
                    providerCredential);
                GenerationAttemptPreparation preparation = await _mediator
                    .Send(command, ct)
                    .ConfigureAwait(false);

                if (preparation is not
                    {
                        IsSuccess: true,
                        Attempt: { } attempt
                    })
                {
                    return _responseWriter.CreatePreparationFailureResponse(
                        preparation,
                        request.Metadata);
                }

                await using (attempt)
                {
                    return await _responseWriter
                        .WriteAsync(HttpContext, attempt, ct)
                        .ConfigureAwait(false);
                }
            }
            catch (GenerationMultipartRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Generation multipart request was rejected.");

                return _responseWriter.CreateProblemResponse(
                    StatusCodes.Status400BadRequest,
                    exception.SafeErrorCode,
                    null,
                    false,
                    exception.LogicalGenerationId,
                    exception.AttemptNumber);
            }
        }
    }
}
