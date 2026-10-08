using System.Net.Http.Headers;
using System.Text.Json;

namespace AtomicArt.Desktop.Services.OpenRouter;

public sealed class OpenRouterBalanceApiClient : IOpenRouterBalanceApiClient
{
    private const int MaximumResponseBytes = 16 * 1024;
    private const int RequestTimeoutSeconds = 10;
    private static readonly Uri CreditsUri = new("https://openrouter.ai/api/v1/credits");
    private readonly HttpClient _httpClient;

    public OpenRouterBalanceApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public static void Configure(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        httpClient.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);
        httpClient.MaxResponseContentBufferSize = MaximumResponseBytes;
    }

    public async Task<decimal> GetBalanceAsync(string managementKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managementKey);

        using HttpRequestMessage request = new(HttpMethod.Get, CreditsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managementKey.Trim());
        using HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream content = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: ct)
            .ConfigureAwait(false);

        if ((document.RootElement.ValueKind != JsonValueKind.Object)
            || (!document.RootElement.TryGetProperty("data", out JsonElement data))
            || (data.ValueKind != JsonValueKind.Object)
            || (!TryReadAmount(data, "total_credits", out decimal credits))
            || (!TryReadAmount(data, "total_usage", out decimal usage)))
        {
            throw new InvalidDataException("OpenRouter returned an invalid account credits response.");
        }

        return credits - usage;
    }

    private static bool TryReadAmount(JsonElement data, string name, out decimal amount)
    {
        amount = default;

        return data.TryGetProperty(name, out JsonElement value)
            && (value.ValueKind == JsonValueKind.Number)
            && value.TryGetDecimal(out amount)
            && (amount >= decimal.Zero);
    }
}
