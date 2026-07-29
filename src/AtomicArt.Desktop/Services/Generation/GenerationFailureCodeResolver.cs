using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationFailureCodeResolver
{
    public static string GetFailureCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            GenerationAttemptException attemptException =>
                Normalize(attemptException.SafeErrorCode),
            HttpRequestException => GenerationClientFailureCodes.ApiUnavailable,
            _ => GenerationClientFailureCodes.Unknown
        };
    }

    public static string Normalize(string? failureCode)
    {
        return string.IsNullOrWhiteSpace(failureCode)
            ? GenerationClientFailureCodes.Unknown
            : failureCode.Trim();
    }
}
