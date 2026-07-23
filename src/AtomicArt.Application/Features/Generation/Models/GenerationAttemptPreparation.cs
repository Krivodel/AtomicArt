namespace AtomicArt.Application.Features.Generation.Models;

public sealed record GenerationAttemptPreparation
{
    public StreamingGenerationAttempt? Attempt { get; }
    public GenerationAttemptPreparationFailureKind? FailureKind { get; }
    public ImageGenerationProviderFailureKind? ProviderFailureKind { get; }
    public string? SafeErrorCode { get; }
    public bool Retryable { get; }
    public bool IsSuccess => Attempt is not null;

    private GenerationAttemptPreparation(
        StreamingGenerationAttempt? attempt,
        GenerationAttemptPreparationFailureKind? failureKind,
        ImageGenerationProviderFailureKind? providerFailureKind,
        string? safeErrorCode,
        bool retryable)
    {
        Attempt = attempt;
        FailureKind = failureKind;
        ProviderFailureKind = providerFailureKind;
        SafeErrorCode = safeErrorCode;
        Retryable = retryable;
    }

    public static GenerationAttemptPreparation Success(StreamingGenerationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return new GenerationAttemptPreparation(attempt, null, null, null, false);
    }

    public static GenerationAttemptPreparation Validation(string safeErrorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);

        return new GenerationAttemptPreparation(
            null,
            GenerationAttemptPreparationFailureKind.Validation,
            null,
            safeErrorCode,
            false);
    }

    public static GenerationAttemptPreparation NotFound(string safeErrorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);

        return new GenerationAttemptPreparation(
            null,
            GenerationAttemptPreparationFailureKind.NotFound,
            null,
            safeErrorCode,
            false);
    }

    public static GenerationAttemptPreparation ProviderFailure(
        ImageGenerationProviderFailureKind providerFailureKind,
        string safeErrorCode,
        bool retryable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeErrorCode);

        return new GenerationAttemptPreparation(
            null,
            GenerationAttemptPreparationFailureKind.Provider,
            providerFailureKind,
            safeErrorCode,
            retryable);
    }
}
