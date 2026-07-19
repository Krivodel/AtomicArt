using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services;

public abstract class AtomicArtApiClient
{
    protected HttpClient HttpClient { get; }
    protected IApiEndpointService ApiEndpointService { get; }
    protected ILogger Logger { get; }

    protected AtomicArtApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(logger);

        HttpClient = httpClient;
        ApiEndpointService = apiEndpointService;
        Logger = logger;
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
