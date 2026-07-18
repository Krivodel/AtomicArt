using System.Text.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

internal static class SafeApiProblemDetailsReader
{
    private const int MaxResponseBytes = 16 * 1024;
    private const int MaxErrorCodeLength = 32;
    private const string ErrorCodePrefix = "ERR-";

    internal static async Task<SafeApiProblemDetailsReadResult> TryReadErrorCodeAsync(
        HttpContent content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            string? errorCode = await ReadErrorCodeAsync(content, ct).ConfigureAwait(false);

            return new SafeApiProblemDetailsReadResult(errorCode, null);
        }
        catch (JsonException exception)
        {
            return new SafeApiProblemDetailsReadResult(null, exception);
        }
        catch (IOException exception)
        {
            return new SafeApiProblemDetailsReadResult(null, exception);
        }
        catch (HttpRequestException exception)
        {
            return new SafeApiProblemDetailsReadResult(null, exception);
        }
    }

    internal static void LogReadFailure(
        ILogger logger,
        SafeApiProblemDetailsReadResult result,
        SafeApiProblemDetailsApi api)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Failure is null)
        {
            return;
        }

        switch (api, result.Failure)
        {
            case (SafeApiProblemDetailsApi.GenerationModelCatalog, JsonException exception):
                logger.LogWarning(
                    exception,
                    "Generation model catalog API returned malformed limited problem details.");
                break;
            case (SafeApiProblemDetailsApi.GenerationModelCatalog, IOException exception):
                logger.LogWarning(
                    exception,
                    "Failed to read limited generation model catalog API problem details.");
                break;
            case (SafeApiProblemDetailsApi.GenerationModelCatalog, HttpRequestException exception):
                logger.LogWarning(
                    exception,
                    "Failed to receive limited generation model catalog API problem details.");
                break;
            case (SafeApiProblemDetailsApi.ImageGeneration, JsonException exception):
                logger.LogWarning(
                    exception,
                    "Image generation API returned malformed limited problem details.");
                break;
            case (SafeApiProblemDetailsApi.ImageGeneration, IOException exception):
                logger.LogWarning(
                    exception,
                    "Failed to read limited image generation API problem details.");
                break;
            case (SafeApiProblemDetailsApi.ImageGeneration, HttpRequestException exception):
                logger.LogWarning(
                    exception,
                    "Failed to receive limited image generation API problem details.");
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported problem details read failure '{result.Failure.GetType().Name}' "
                        + $"for API '{api}'.");
        }
    }

    internal static async Task LogResponseFailureAsync(
        ILogger logger,
        HttpResponseMessage response,
        SafeApiProblemDetailsApi api,
        Action<string> logResponseFailure,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(logResponseFailure);

        SafeApiProblemDetailsReadResult problemDetails = await TryReadErrorCodeAsync(
                response.Content,
                ct)
            .ConfigureAwait(false);
        LogReadFailure(logger, problemDetails, api);
        logResponseFailure(problemDetails.LogErrorCode);
    }

    private static async Task<string?> ReadErrorCodeAsync(
        HttpContent content,
        CancellationToken ct)
    {
        string? mediaType = content.Headers.ContentType?.MediaType;

        if (mediaType is null
            || (!mediaType.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
                && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        await using Stream stream = await content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        byte[] buffer = new byte[MaxResponseBytes + 1];
        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead = await stream
                .ReadAsync(buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        if (totalBytesRead == 0
            || totalBytesRead > MaxResponseBytes)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(buffer.AsMemory(0, totalBytesRead));

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName,
                out JsonElement errorCodeElement)
            || errorCodeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? errorCode = errorCodeElement.GetString();

        return IsSafeErrorCode(errorCode)
            ? errorCode
            : null;
    }

    private static bool IsSafeErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode)
            || errorCode.Length > MaxErrorCodeLength
            || !errorCode.StartsWith(ErrorCodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return errorCode.All(character =>
            character is >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-');
    }
}
