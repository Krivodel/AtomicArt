namespace AtomicArt.Desktop.Services;

internal sealed record SafeApiProblemDetailsReadResult(
    string? ErrorCode,
    Exception? Failure);
