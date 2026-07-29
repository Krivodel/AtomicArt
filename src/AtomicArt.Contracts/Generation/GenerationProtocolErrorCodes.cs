namespace AtomicArt.Contracts.Generation;

public static class GenerationProtocolErrorCodes
{
    public const string ModelNotFound = "GENERATION_MODEL_NOT_FOUND";
    public const string UnsupportedResolution = "GENERATION_UNSUPPORTED_RESOLUTION";
    public const string UnsupportedAspectRatio = "GENERATION_UNSUPPORTED_ASPECT_RATIO";
    public const string ModelRequestValidation = "GENERATION_MODEL_REQUEST_VALIDATION";
    public const string ConcurrencyLimitReached = "GENERATION_CONCURRENCY_LIMIT_REACHED";
    public const string InvalidMultipartRequest = "GENERATION_INVALID_MULTIPART_REQUEST";
    public const string InvalidAttemptNumber = "GENERATION_INVALID_ATTEMPT_NUMBER";
    public const string InvalidParameters = "GENERATION_INVALID_PARAMETERS";
    public const string ResponseTooLarge = "GENERATION_RESPONSE_TOO_LARGE";
    public const string TransportInterrupted = "GENERATION_TRANSPORT_INTERRUPTED";
}
