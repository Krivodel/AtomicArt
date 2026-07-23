namespace AtomicArt.Api.Generation;

public sealed class GenerationMultipartRequestException : IOException
{
    public string SafeErrorCode { get; }
    public Guid? LogicalGenerationId { get; }
    public int? AttemptNumber { get; }

    public GenerationMultipartRequestException(
        string safeErrorCode,
        string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);

        SafeErrorCode = safeErrorCode;
    }

    public GenerationMultipartRequestException(
        string safeErrorCode,
        string message,
        Guid logicalGenerationId,
        int attemptNumber)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);

        SafeErrorCode = safeErrorCode;
        LogicalGenerationId = logicalGenerationId;
        AttemptNumber = attemptNumber;
    }

    public GenerationMultipartRequestException(
        string safeErrorCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);
        ArgumentNullException.ThrowIfNull(innerException);

        SafeErrorCode = safeErrorCode;
    }
}
