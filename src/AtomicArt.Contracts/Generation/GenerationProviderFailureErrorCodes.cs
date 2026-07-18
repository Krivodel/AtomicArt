namespace AtomicArt.Contracts.Generation;

public static class GenerationProviderFailureErrorCodes
{
    public const string Authentication = "ERR-GEN-005";
    public const string Authorization = "ERR-GEN-006";
    public const string RateLimited = "ERR-GEN-007";
    public const string InvalidResponse = "ERR-GEN-008";
    public const string Timeout = "ERR-GEN-009";
    public const string Unavailable = "ERR-GEN-010";
    public const string RequestRejected = "ERR-GEN-011";
    public const string ResourceNotFound = "ERR-GEN-012";
    public const string InternalError = "ERR-GEN-013";
    public const string Unknown = "ERR-GEN-014";
}
