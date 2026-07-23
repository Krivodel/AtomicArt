using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Generation;

public sealed class GenerationStreamingResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ILogger<GenerationStreamingResponseWriter> _logger;

    public GenerationStreamingResponseWriter(
        ILogger<GenerationStreamingResponseWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IActionResult> WriteAsync(
        HttpContext httpContext,
        StreamingGenerationAttempt attempt,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(attempt);

        string boundary = $"atomicart-{Guid.NewGuid():N}";
        HttpResponse response = httpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = $"multipart/mixed; boundary=\"{boundary}\"";
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await WriteProviderPartHeadersAsync(
                    response,
                    boundary,
                    attempt.ProviderResponseContentType,
                    attempt.ProviderId,
                    ct)
                .ConfigureAwait(false);
            GenerationAttemptMetadataDto metadata =
                await attempt.CopyProviderResponseAsync(response.Body, ct)
                    .ConfigureAwait(false);
            await WriteMetadataPartAsync(response, boundary, metadata, ct)
                .ConfigureAwait(false);

            return new EmptyResult();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            httpContext.Abort();

            return new EmptyResult();
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Streaming generation response was interrupted after the response started.");
            httpContext.Abort();

            return new EmptyResult();
        }
    }

    public IActionResult CreatePreparationFailureResponse(
        GenerationAttemptPreparation preparation,
        GenerationRequestMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(metadata);

        int statusCode = preparation.FailureKind switch
        {
            GenerationAttemptPreparationFailureKind.Validation
                => StatusCodes.Status400BadRequest,
            GenerationAttemptPreparationFailureKind.NotFound
                => StatusCodes.Status400BadRequest,
            GenerationAttemptPreparationFailureKind.Provider
                => GetProviderFailureStatusCode(preparation.ProviderFailureKind),
            _ => StatusCodes.Status500InternalServerError
        };

        return CreateProblemResponse(
            statusCode,
            preparation.SafeErrorCode,
            preparation.ProviderFailureKind,
            preparation.Retryable,
            metadata.LogicalGenerationId,
            metadata.AttemptNumber);
    }

    public IActionResult CreateProblemResponse(
        int statusCode,
        string? safeErrorCode,
        ImageGenerationProviderFailureKind? providerFailureKind,
        bool retryable,
        Guid? logicalGenerationId,
        int? attemptNumber)
    {
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = "Ошибка запроса генерации.",
            Detail = "Запрос генерации не удалось обработать."
        };

        if (!string.IsNullOrWhiteSpace(safeErrorCode))
        {
            problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName] =
                safeErrorCode;
        }

        problemDetails.Extensions[
            GenerationApiRoutes.ProblemDetailsRetryableExtensionName] =
            retryable;

        if (providerFailureKind.HasValue)
        {
            problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsProviderFailureKindExtensionName] =
                ToContractFailureKind(providerFailureKind.Value);
        }

        if (logicalGenerationId.HasValue)
        {
            problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsLogicalGenerationIdExtensionName] =
                logicalGenerationId.Value;
        }

        if (attemptNumber.HasValue)
        {
            problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsAttemptNumberExtensionName] =
                attemptNumber.Value;
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    private static async Task WriteProviderPartHeadersAsync(
        HttpResponse response,
        string boundary,
        string contentType,
        string providerId,
        CancellationToken ct)
    {
        if (!System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(
                contentType,
                out System.Net.Http.Headers.MediaTypeHeaderValue? parsedContentType)
            || providerId.Any(character =>
                character is not (>= 'a' and <= 'z')
                    and not (>= '0' and <= '9')
                    and not (>= 'A' and <= 'Z')
                    and not '_'
                    and not '.'
                    and not '-'))
        {
            throw new InvalidDataException(
                "Provider response headers are invalid.");
        }

        string headers =
            $"--{boundary}\r\n"
            + $"Content-Disposition: inline; name=\"{GenerationApiRoutes.ProviderResponsePartName}\"\r\n"
            + $"Content-Type: {parsedContentType}\r\n"
            + $"X-AtomicArt-Provider-Id: {providerId}\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

        await response.Body.WriteAsync(headerBytes, ct).ConfigureAwait(false);
    }

    private static async Task WriteMetadataPartAsync(
        HttpResponse response,
        string boundary,
        GenerationAttemptMetadataDto metadata,
        CancellationToken ct)
    {
        string headers =
            $"\r\n--{boundary}\r\n"
            + $"Content-Disposition: inline; name=\"{GenerationApiRoutes.GenerationMetadataPartName}\"\r\n"
            + "Content-Type: application/json\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

        await response.Body.WriteAsync(headerBytes, ct).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(
                response.Body,
                metadata,
                SerializerOptions,
                ct)
            .ConfigureAwait(false);
        byte[] closingBoundary = Encoding.ASCII.GetBytes(
            $"\r\n--{boundary}--\r\n");
        await response.Body
            .WriteAsync(closingBoundary, ct)
            .ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private static int GetProviderFailureStatusCode(
        ImageGenerationProviderFailureKind? failureKind)
    {
        return failureKind switch
        {
            ImageGenerationProviderFailureKind.Authentication
                => StatusCodes.Status401Unauthorized,
            ImageGenerationProviderFailureKind.Authorization
                => StatusCodes.Status403Forbidden,
            ImageGenerationProviderFailureKind.RateLimited
                => StatusCodes.Status429TooManyRequests,
            ImageGenerationProviderFailureKind.Timeout
                => StatusCodes.Status504GatewayTimeout,
            ImageGenerationProviderFailureKind.Unavailable
                => StatusCodes.Status503ServiceUnavailable,
            ImageGenerationProviderFailureKind.RequestRejected
                or ImageGenerationProviderFailureKind.ResourceNotFound
                => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status502BadGateway
        };
    }

    private static string ToContractFailureKind(
        ImageGenerationProviderFailureKind failureKind)
    {
        return failureKind switch
        {
            ImageGenerationProviderFailureKind.Authentication
                => "authentication",
            ImageGenerationProviderFailureKind.Authorization
                => "authorization",
            ImageGenerationProviderFailureKind.RateLimited
                => "rate_limited",
            ImageGenerationProviderFailureKind.InvalidResponse
                => "invalid_response",
            ImageGenerationProviderFailureKind.Timeout
                => "timeout",
            ImageGenerationProviderFailureKind.Unavailable
                => "unavailable",
            ImageGenerationProviderFailureKind.RequestRejected
                => "request_rejected",
            ImageGenerationProviderFailureKind.ResourceNotFound
                => "resource_not_found",
            ImageGenerationProviderFailureKind.InternalError
                => "internal_error",
            _ => "unknown"
        };
    }
}
