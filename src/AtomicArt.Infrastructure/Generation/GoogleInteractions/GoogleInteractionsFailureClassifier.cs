using System.Net;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsFailureClassifier
{
    public ImageGenerationProviderFailureKind GetFailureKind(
        HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest
                => ImageGenerationProviderFailureKind.RequestRejected,
            HttpStatusCode.Unauthorized
                => ImageGenerationProviderFailureKind.Authentication,
            HttpStatusCode.Forbidden
                => ImageGenerationProviderFailureKind.Authorization,
            HttpStatusCode.NotFound
                => ImageGenerationProviderFailureKind.ResourceNotFound,
            (HttpStatusCode)429
                => ImageGenerationProviderFailureKind.RateLimited,
            HttpStatusCode.InternalServerError
                => ImageGenerationProviderFailureKind.InternalError,
            HttpStatusCode.BadGateway
                => ImageGenerationProviderFailureKind.InvalidResponse,
            HttpStatusCode.ServiceUnavailable
                => ImageGenerationProviderFailureKind.Unavailable,
            HttpStatusCode.GatewayTimeout
                => ImageGenerationProviderFailureKind.Timeout,
            _ => ImageGenerationProviderFailureKind.Unknown
        };
    }

    public bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    public bool IsTemporaryInternalError(JsonElement root)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
                root,
                "error",
                out JsonElement error)
            || error.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasInternalCode(error, "status")
            || HasInternalCode(error, "code");
    }

    private static bool HasInternalCode(
        JsonElement error,
        string propertyName)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
                error,
                propertyName,
                out JsonElement code))
        {
            return false;
        }

        if (code.ValueKind == JsonValueKind.Number
            && code.TryGetInt32(out int numericCode))
        {
            return numericCode == 500;
        }

        if (code.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? value = code.GetString();

        return string.Equals(value, "INTERNAL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                value,
                "INTERNAL_ERROR",
                StringComparison.OrdinalIgnoreCase);
    }
}
