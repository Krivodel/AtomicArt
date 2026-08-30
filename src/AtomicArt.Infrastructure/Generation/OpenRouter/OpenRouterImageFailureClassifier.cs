using System.Net;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageFailureClassifier
{
    public ImageGenerationProviderFailureKind GetFailureKind(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ImageGenerationProviderFailureKind.RequestRejected,
            HttpStatusCode.Unauthorized => ImageGenerationProviderFailureKind.Authentication,
            HttpStatusCode.Forbidden => ImageGenerationProviderFailureKind.Authorization,
            HttpStatusCode.NotFound => ImageGenerationProviderFailureKind.ResourceNotFound,
            (HttpStatusCode)429 => ImageGenerationProviderFailureKind.RateLimited,
            HttpStatusCode.InternalServerError => ImageGenerationProviderFailureKind.InternalError,
            HttpStatusCode.BadGateway => ImageGenerationProviderFailureKind.InvalidResponse,
            HttpStatusCode.ServiceUnavailable => ImageGenerationProviderFailureKind.Unavailable,
            HttpStatusCode.GatewayTimeout => ImageGenerationProviderFailureKind.Timeout,
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
}
