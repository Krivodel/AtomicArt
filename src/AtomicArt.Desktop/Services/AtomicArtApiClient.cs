using System.Net.Http.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services;

public abstract class AtomicArtApiClient
{
    protected HttpClient HttpClient { get; }
    protected IApiEndpointService ApiEndpointService { get; }
    protected ILogger Logger { get; }
    protected int MaximumProblemDetailsErrorCodeCharacters { get; }
    protected int MaximumProblemDetailsResponseBytes { get; }

    protected AtomicArtApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger logger,
        IOptions<ApiClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        HttpClient = httpClient;
        ApiEndpointService = apiEndpointService;
        Logger = logger;
        MaximumProblemDetailsErrorCodeCharacters =
            options.Value.MaximumProblemDetailsErrorCodeCharacters;
        MaximumProblemDetailsResponseBytes =
            options.Value.MaximumProblemDetailsResponseBytes;
    }

    private protected async Task<TResponse> ReadSuccessfulJsonResponseAsync<TResponse>(
        HttpResponseMessage response,
        SafeApiProblemDetailsApi api,
        Action<string> logResponseFailure,
        string emptyResponseMessage,
        CancellationToken ct)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(logResponseFailure);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyResponseMessage);

        if (!response.IsSuccessStatusCode)
        {
            await SafeApiProblemDetailsReader
                .LogResponseFailureAsync(
                    Logger,
                    response,
                    api,
                    logResponseFailure,
                    MaximumProblemDetailsResponseBytes,
                    MaximumProblemDetailsErrorCodeCharacters,
                    ct)
                .ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();

        TResponse? result = await response.Content
            .ReadFromJsonAsync<TResponse>(ct)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException(emptyResponseMessage);
    }
}
