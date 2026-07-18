using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public static class ImageGenerationProviderFailureCatalog
{
    private static readonly IReadOnlyDictionary<ImageGenerationProviderFailureKind, string>
        ErrorCodesByFailureKind =
            new Dictionary<ImageGenerationProviderFailureKind, string>
            {
                [ImageGenerationProviderFailureKind.Authentication] =
                    GenerationProviderFailureErrorCodes.Authentication,
                [ImageGenerationProviderFailureKind.Authorization] =
                    GenerationProviderFailureErrorCodes.Authorization,
                [ImageGenerationProviderFailureKind.RateLimited] =
                    GenerationProviderFailureErrorCodes.RateLimited,
                [ImageGenerationProviderFailureKind.InvalidResponse] =
                    GenerationProviderFailureErrorCodes.InvalidResponse,
                [ImageGenerationProviderFailureKind.Timeout] =
                    GenerationProviderFailureErrorCodes.Timeout,
                [ImageGenerationProviderFailureKind.Unavailable] =
                    GenerationProviderFailureErrorCodes.Unavailable,
                [ImageGenerationProviderFailureKind.RequestRejected] =
                    GenerationProviderFailureErrorCodes.RequestRejected,
                [ImageGenerationProviderFailureKind.ResourceNotFound] =
                    GenerationProviderFailureErrorCodes.ResourceNotFound,
                [ImageGenerationProviderFailureKind.InternalError] =
                    GenerationProviderFailureErrorCodes.InternalError,
                [ImageGenerationProviderFailureKind.Unknown] =
                    GenerationProviderFailureErrorCodes.Unknown
            };

    public static string GetErrorCode(ImageGenerationProviderFailureKind failureKind)
    {
        return ErrorCodesByFailureKind.TryGetValue(failureKind, out string? errorCode)
            ? errorCode
            : ErrorCodesByFailureKind[ImageGenerationProviderFailureKind.Unknown];
    }

    public static bool TryGetFailureKind(
        string? errorCode,
        out ImageGenerationProviderFailureKind failureKind)
    {
        foreach (KeyValuePair<ImageGenerationProviderFailureKind, string> definition
            in ErrorCodesByFailureKind)
        {
            if (string.Equals(definition.Value, errorCode, StringComparison.Ordinal))
            {
                failureKind = definition.Key;
                return true;
            }
        }

        failureKind = default;
        return false;
    }
}
