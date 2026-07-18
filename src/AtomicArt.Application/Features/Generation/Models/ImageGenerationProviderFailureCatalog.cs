namespace AtomicArt.Application.Features.Generation.Models;

public static class ImageGenerationProviderFailureCatalog
{
    private static readonly IReadOnlyDictionary<ImageGenerationProviderFailureKind, string>
        ErrorCodesByFailureKind =
            new Dictionary<ImageGenerationProviderFailureKind, string>
            {
                [ImageGenerationProviderFailureKind.Authentication] = "ERR-GEN-005",
                [ImageGenerationProviderFailureKind.Authorization] = "ERR-GEN-006",
                [ImageGenerationProviderFailureKind.RateLimited] = "ERR-GEN-007",
                [ImageGenerationProviderFailureKind.InvalidResponse] = "ERR-GEN-008",
                [ImageGenerationProviderFailureKind.Timeout] = "ERR-GEN-009",
                [ImageGenerationProviderFailureKind.Unavailable] = "ERR-GEN-010",
                [ImageGenerationProviderFailureKind.RequestRejected] = "ERR-GEN-011",
                [ImageGenerationProviderFailureKind.ResourceNotFound] = "ERR-GEN-012",
                [ImageGenerationProviderFailureKind.InternalError] = "ERR-GEN-013",
                [ImageGenerationProviderFailureKind.Unknown] = "ERR-GEN-014"
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
