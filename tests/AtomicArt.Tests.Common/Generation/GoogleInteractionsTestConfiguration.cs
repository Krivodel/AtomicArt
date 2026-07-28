using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Tests.Common.Generation;

public static class GoogleInteractionsTestConfiguration
{
    public const string BaseUrl =
        "https://generativelanguage.googleapis.com";

    public static Dictionary<string, string?> Create()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [CreateKey(nameof(GoogleInteractionsOptions.BaseUrl))] =
                BaseUrl,
            [CreateKey(nameof(GoogleInteractionsOptions.Base64InputBufferSize))] =
                "48",
            [CreateKey(nameof(GoogleInteractionsOptions.Base64OutputBufferSize))] =
                "64",
            [CreateKey(nameof(GoogleInteractionsOptions.InteractionsPath))] =
                "/v1beta/interactions",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxAnalyzedMetadataBytes))] =
                "4096",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxDiagnosticTextCharacters))] =
                "512",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxLoggedErrorMessageCharacters))] =
                "512",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxRequestBytes))] =
                "1048576",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxResponseBytes))] =
                "1048576",
            [CreateKey(nameof(GoogleInteractionsOptions.MaxResponseStructureDepth))] =
                "64",
            [CreateKey(nameof(GoogleInteractionsOptions.ProviderResponseTimeoutSeconds))] =
                "900",
            [CreateKey(nameof(GoogleInteractionsOptions.ResponseBufferSize))] =
                "4096",
            [CreateKey(nameof(GoogleInteractionsOptions.ServiceTier))] =
                "flex",
            [CreateKey(nameof(GoogleInteractionsOptions.StoreInteractions))] =
                "true"
        };
    }

    private static string CreateKey(string key)
    {
        return $"{GoogleInteractionsOptions.SectionName}:{key}";
    }
}
