namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

public sealed class GoogleInteractionsOptions
{
    public const string SectionName = "GoogleInteractions";

    public string BaseUrl { get; init; } = string.Empty;
    public int Base64InputBufferSize { get; init; }
    public int Base64OutputBufferSize { get; init; }
    public string InteractionsPath { get; init; } = string.Empty;
    public long MaxRequestBytes { get; init; }
    public long MaxResponseBytes { get; init; }
    public int MaxAnalyzedMetadataBytes { get; init; }
    public int MaxLoggedErrorMessageCharacters { get; init; }
    public int MaxResponseStructureDepth { get; init; }
    public int MaxDiagnosticTextCharacters { get; init; }
    public int ProviderResponseTimeoutSeconds { get; init; }
    public int ResponseBufferSize { get; init; }
    public string ServiceTier { get; init; } = string.Empty;
    public bool StoreInteractions { get; init; }

    private const string AllowedBaseUrlHost =
        "generativelanguage.googleapis.com";

    public static bool IsValid(GoogleInteractionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsValidBase64BufferSizes(options)
            && IsValidBaseUrl(options.BaseUrl)
            && IsValidInteractionsPath(options.InteractionsPath)
            && options.MaxRequestBytes > 0
            && options.MaxResponseBytes > 0
            && options.MaxAnalyzedMetadataBytes > 0
            && options.MaxLoggedErrorMessageCharacters > 0
            && options.MaxResponseStructureDepth > 0
            && options.MaxDiagnosticTextCharacters > 0
            && options.ProviderResponseTimeoutSeconds > 0
            && options.ResponseBufferSize > 0
            && !string.IsNullOrWhiteSpace(options.ServiceTier);
    }

    private static bool IsValidBase64BufferSizes(
        GoogleInteractionsOptions options)
    {
        return options.Base64InputBufferSize > 0
            && options.Base64InputBufferSize % 3 == 0
            && options.Base64OutputBufferSize
                >= options.Base64InputBufferSize / 3 * 4;
    }

    private static bool IsValidBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && string.Equals(uri.Host, AllowedBaseUrlHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidInteractionsPath(string interactionsPath)
    {
        return !string.IsNullOrWhiteSpace(interactionsPath)
            && interactionsPath.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(interactionsPath, UriKind.Relative, out _);
    }
}
