namespace AtomicArt.Domain.Generation;

public sealed record GenerationValidationResult
{
    public bool IsValid { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private GenerationValidationResult(bool isValid, string? errorCode, string? errorMessage)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static GenerationValidationResult Valid()
    {
        return new GenerationValidationResult(true, null, null);
    }

    public static GenerationValidationResult Invalid(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new GenerationValidationResult(false, errorCode, errorMessage);
    }
}
