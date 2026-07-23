namespace AtomicArt.Contracts.Generation;

public static class GenerationProtocolErrorCodes
{
    public const string ConcurrencyLimitReached = "GENERATION_CONCURRENCY_LIMIT_REACHED";
    public const string InvalidMultipartRequest = "GENERATION_INVALID_MULTIPART_REQUEST";
    public const string InvalidAttemptNumber = "GENERATION_INVALID_ATTEMPT_NUMBER";
    public const string InvalidParameters = "GENERATION_INVALID_PARAMETERS";
    public const string ResponseTooLarge = "GENERATION_RESPONSE_TOO_LARGE";
    public const string TransportInterrupted = "GENERATION_TRANSPORT_INTERRUPTED";
}
