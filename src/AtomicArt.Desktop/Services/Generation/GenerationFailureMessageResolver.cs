using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationFailureMessageResolver
{
    private static readonly IReadOnlyDictionary<string, string> MessagesByErrorCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GenerationProtocolErrorCodes.ModelNotFound] =
                GenerationUiLocalizationKeys.Errors.ModelNotFound,
            [GenerationProtocolErrorCodes.UnsupportedResolution] =
                GenerationUiLocalizationKeys.Errors.UnsupportedResolution,
            [GenerationProtocolErrorCodes.UnsupportedAspectRatio] =
                GenerationUiLocalizationKeys.Errors.UnsupportedAspectRatio,
            [GenerationProtocolErrorCodes.ModelRequestValidation] =
                GenerationUiLocalizationKeys.Errors.ModelRequestValidation,
            [GenerationProviderFailureErrorCodes.Authentication] =
                GenerationUiLocalizationKeys.Errors.AuthenticationFailed,
            [GenerationProviderFailureErrorCodes.Authorization] =
                GenerationUiLocalizationKeys.Errors.AuthorizationFailed,
            [GenerationProviderFailureErrorCodes.RateLimited] =
                GenerationUiLocalizationKeys.Errors.RateLimited,
            [GenerationProviderFailureErrorCodes.InvalidResponse] =
                GenerationUiLocalizationKeys.Errors.InvalidResponse,
            [GenerationProviderFailureErrorCodes.Timeout] =
                GenerationUiLocalizationKeys.Errors.TimedOut,
            [GenerationProviderFailureErrorCodes.Unavailable] =
                GenerationUiLocalizationKeys.Errors.ProviderUnavailable,
            [GenerationProviderFailureErrorCodes.RequestRejected] =
                GenerationUiLocalizationKeys.Errors.RequestRejected,
            [GenerationProviderFailureErrorCodes.ResourceNotFound] =
                GenerationUiLocalizationKeys.Errors.ResourceNotFound,
            [GenerationProviderFailureErrorCodes.InternalError] =
                GenerationUiLocalizationKeys.Errors.ProviderInternalError,
            [GenerationProviderFailureErrorCodes.Unknown] =
                GenerationUiLocalizationKeys.Errors.Failed,
            [GenerationProtocolErrorCodes.ConcurrencyLimitReached] =
                GenerationUiLocalizationKeys.Errors.ConcurrencyLimitReached,
            [GenerationProtocolErrorCodes.InvalidMultipartRequest] =
                GenerationUiLocalizationKeys.Errors.InvalidRequest,
            [GenerationProtocolErrorCodes.InvalidAttemptNumber] =
                GenerationUiLocalizationKeys.Errors.InvalidAttempt,
            [GenerationProtocolErrorCodes.InvalidParameters] =
                GenerationUiLocalizationKeys.Errors.InvalidParameters,
            [GenerationProtocolErrorCodes.ResponseTooLarge] =
                GenerationUiLocalizationKeys.Errors.ResponseTooLarge,
            [GenerationProtocolErrorCodes.TransportInterrupted] =
                GenerationUiLocalizationKeys.Errors.TransportInterrupted,
            [GenerationClientFailureCodes.ApiUnavailable] =
                GenerationUiLocalizationKeys.Errors.ApiUnavailable,
            [GenerationClientFailureCodes.Unknown] =
                GenerationUiLocalizationKeys.Errors.Failed
        };

    public static string GetLocalizationKey(string? failureCode)
    {
        string normalizedFailureCode =
            GenerationFailureCodeResolver.Normalize(failureCode);

        return MessagesByErrorCode.TryGetValue(
            normalizedFailureCode,
            out string? localizationKey)
            ? localizationKey
            : GenerationUiLocalizationKeys.Errors.Failed;
    }

    public static string GetLocalizationKey(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return GetLocalizationKey(
            GenerationFailureCodeResolver.GetFailureCode(exception));
    }
}
