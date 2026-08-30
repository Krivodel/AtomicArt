using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageException : ImageGenerationProviderException
{
    public OpenRouterImageException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        bool retryable = false,
        Exception? innerException = null)
        : base(
            failureKind,
            message,
            retryable,
            innerException ?? new InvalidOperationException(message))
    {
    }
}
