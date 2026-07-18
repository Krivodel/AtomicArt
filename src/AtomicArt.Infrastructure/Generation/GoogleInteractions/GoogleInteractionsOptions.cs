namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

public sealed class GoogleInteractionsOptions
{
    public const string SectionName = "GoogleInteractions";
    public const string AllowedBaseUrlHost = "generativelanguage.googleapis.com";

    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; }

    public static bool IsValid(GoogleInteractionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsValidBaseUrl(options.BaseUrl)
            && options.TimeoutSeconds > 0;
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
