namespace AtomicArt.Infrastructure.Generation.OpenRouter;

public sealed class OpenRouterImageOptions
{
    public const string SectionName = "OpenRouterImage";

    public string BaseUrl { get; init; } = string.Empty;
    public string ChatCompletionsPath { get; init; } = string.Empty;
    public string ImagesPath { get; init; } = string.Empty;
    public long MaxRequestBytes { get; init; }
    public long MaxResponseBytes { get; init; }
    public int ProviderResponseTimeoutSeconds { get; init; }
    public int ResponseBufferSize { get; init; }

    public static bool IsValid(OpenRouterImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri)
            && string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && string.Equals(baseUri.Host, "openrouter.ai", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.ChatCompletionsPath)
            && options.ChatCompletionsPath.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(options.ChatCompletionsPath, UriKind.Relative, out _)
            && !string.IsNullOrWhiteSpace(options.ImagesPath)
            && options.ImagesPath.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(options.ImagesPath, UriKind.Relative, out _)
            && options.MaxRequestBytes > 0
            && options.MaxResponseBytes > 0
            && options.ProviderResponseTimeoutSeconds > 0
            && options.ResponseBufferSize > 0;
    }
}
