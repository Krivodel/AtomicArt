namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationClientFailureCodes
{
    public const string ApiUnavailable = "GENERATION_CLIENT_API_UNAVAILABLE";
    public const string CodexUnavailable = "GENERATION_CLIENT_CODEX_UNAVAILABLE";
    public const string CodexSignInRequired = "GENERATION_CLIENT_CODEX_SIGN_IN_REQUIRED";
    public const string CodexNoImage = "GENERATION_CLIENT_CODEX_NO_IMAGE";
    public const string Unknown = "GENERATION_CLIENT_UNKNOWN";
}
