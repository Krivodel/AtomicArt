namespace AtomicArt.Desktop.Services;

internal sealed record SafeApiProblemDetailsReadResult(
    string? ErrorCode,
    Exception? Failure)
{
    public string LogErrorCode => ErrorCode ?? "unavailable";
}
