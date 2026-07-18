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
}
