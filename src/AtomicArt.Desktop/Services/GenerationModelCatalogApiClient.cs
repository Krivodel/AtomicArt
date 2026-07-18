using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class GenerationModelCatalogApiClient
    : AtomicArtApiClient, IGenerationModelCatalogApiClient
{
    public GenerationModelCatalogApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger<GenerationModelCatalogApiClient> logger)
        : base(httpClient, apiEndpointService, logger)
    {
    }

    public async Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("Loading generation model catalog from Atomic Art API.");

        Uri requestUri = ApiEndpointService.CreateRequestUri(GenerationApiRoutes.Models);
        using HttpResponseMessage response = await HttpClient
            .GetAsync(requestUri, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await SafeApiProblemDetailsReader
                .LogResponseFailureAsync(
                    Logger,
                    response,
                    SafeApiProblemDetailsApi.GenerationModelCatalog,
                    errorCode => Logger.LogWarning(
                        "Generation model catalog API returned HTTP {StatusCode} with error code {ErrorCode} after {ElapsedMilliseconds} ms.",
                        (int)response.StatusCode,
                        errorCode,
                        stopwatch.ElapsedMilliseconds),
                    ct)
                .ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();

        GenerationModelCatalogDto? catalog = await response.Content
            .ReadFromJsonAsync<GenerationModelCatalogDto>(ct)
            .ConfigureAwait(false);

        if (catalog is null)
        {
            throw new InvalidOperationException("Generation model catalog API returned an empty response.");
        }

        Logger.LogInformation(
            "Generation model catalog loaded with {ModelCount} models after {ElapsedMilliseconds} ms.",
            catalog.Models.Count,
            stopwatch.ElapsedMilliseconds);

        return catalog;
    }
}
