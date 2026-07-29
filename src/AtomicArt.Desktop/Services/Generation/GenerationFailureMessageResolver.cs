using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationFailureMessageResolver
{
    private static readonly IReadOnlyDictionary<string, string> MessagesByErrorCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GenerationProviderFailureErrorCodes.Authentication] =
                UiStrings.GenerationAuthenticationFailed,
            [GenerationProviderFailureErrorCodes.Authorization] =
                UiStrings.GenerationAuthorizationFailed,
            [GenerationProviderFailureErrorCodes.RateLimited] =
                UiStrings.GenerationRateLimited,
            [GenerationProviderFailureErrorCodes.InvalidResponse] =
                UiStrings.GenerationInvalidResponse,
            [GenerationProviderFailureErrorCodes.Timeout] =
                UiStrings.GenerationTimedOut,
            [GenerationProviderFailureErrorCodes.Unavailable] =
                UiStrings.GenerationProviderUnavailable,
            [GenerationProviderFailureErrorCodes.RequestRejected] =
                UiStrings.GenerationRequestRejected,
            [GenerationProviderFailureErrorCodes.ResourceNotFound] =
                UiStrings.GenerationResourceNotFound,
            [GenerationProviderFailureErrorCodes.InternalError] =
                UiStrings.GenerationProviderInternalError,
            [GenerationProviderFailureErrorCodes.Unknown] =
                UiStrings.GenerationFailed,
            [GenerationProtocolErrorCodes.ConcurrencyLimitReached] =
                UiStrings.GenerationConcurrencyLimitReached,
            [GenerationProtocolErrorCodes.InvalidMultipartRequest] =
                UiStrings.GenerationInvalidRequest,
            [GenerationProtocolErrorCodes.InvalidAttemptNumber] =
                UiStrings.GenerationInvalidAttempt,
            [GenerationProtocolErrorCodes.InvalidParameters] =
                UiStrings.GenerationInvalidParameters,
            [GenerationProtocolErrorCodes.ResponseTooLarge] =
                UiStrings.GenerationResponseTooLarge,
            [GenerationProtocolErrorCodes.TransportInterrupted] =
                UiStrings.GenerationTransportInterrupted,
            [GenerationClientFailureCodes.ApiUnavailable] =
                UiStrings.GenerationApiUnavailable,
            [GenerationClientFailureCodes.Unknown] =
                UiStrings.GenerationFailed
        };

    public static string GetUserMessage(string? failureCode)
    {
        string normalizedFailureCode = GenerationFailureCodeResolver.Normalize(failureCode);

        return MessagesByErrorCode.TryGetValue(normalizedFailureCode, out string? message)
            ? message
            : UiStrings.GenerationFailed;
    }

    public static string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return GetUserMessage(
            GenerationFailureCodeResolver.GetFailureCode(exception));
    }
}
