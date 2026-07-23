namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

public sealed class GoogleInteractionsOptions
{
    public const string SectionName = "GoogleInteractions";
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com";
    public const long DefaultMaxRequestBytes = 800L * 1024L * 1024L;
    public const long DefaultMaxResponseBytes = 1024L * 1024L * 1024L;
    public const int DefaultMaxAnalyzedMetadataBytes = 4 * 1024 * 1024;
    public const int DefaultMaxResponseStructureDepth = 64;
    public const int DefaultMaxDiagnosticTextCharacters = 512;

    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public int TimeoutSeconds { get; init; }
    public long MaxRequestBytes { get; init; } = DefaultMaxRequestBytes;
    public long MaxResponseBytes { get; init; } = DefaultMaxResponseBytes;
    public int MaxAnalyzedMetadataBytes { get; init; } =
        DefaultMaxAnalyzedMetadataBytes;
    public int MaxResponseStructureDepth { get; init; } =
        DefaultMaxResponseStructureDepth;
    public int MaxDiagnosticTextCharacters { get; init; } =
        DefaultMaxDiagnosticTextCharacters;

    private static readonly string AllowedBaseUrlHost = new Uri(DefaultBaseUrl).Host;

    public static bool IsValid(GoogleInteractionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsValidBaseUrl(options.BaseUrl)
            && options.TimeoutSeconds > 0
            && options.MaxRequestBytes > 0
            && options.MaxResponseBytes > 0
            && options.MaxAnalyzedMetadataBytes > 0
            && options.MaxResponseStructureDepth > 0
            && options.MaxDiagnosticTextCharacters > 0;
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
}
