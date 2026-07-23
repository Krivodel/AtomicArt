namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationAttemptException : InvalidOperationException
{
    public string? SafeErrorCode { get; }
    public bool Retryable { get; }

    public GenerationAttemptException(
        string message,
        string? safeErrorCode,
        bool retryable)
        : base(message)
    {
        SafeErrorCode = safeErrorCode;
        Retryable = retryable;
    }

    public GenerationAttemptException(
        string message,
        string? safeErrorCode,
        bool retryable,
        Exception innerException)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);

        SafeErrorCode = safeErrorCode;
        Retryable = retryable;
    }
}
