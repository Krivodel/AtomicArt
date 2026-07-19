using System.Diagnostics;
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

        GenerationModelCatalogDto catalog = await ReadSuccessfulJsonResponseAsync<GenerationModelCatalogDto>(
                response,
                SafeApiProblemDetailsApi.GenerationModelCatalog,
                errorCode => Logger.LogWarning(
                    "Generation model catalog API returned HTTP {StatusCode} with error code {ErrorCode} after {ElapsedMilliseconds} ms.",
                    (int)response.StatusCode,
                    errorCode,
                    stopwatch.ElapsedMilliseconds),
                "Generation model catalog API returned an empty response.",
                ct)
            .ConfigureAwait(false);

        Logger.LogInformation(
            "Generation model catalog loaded with {ModelCount} models after {ElapsedMilliseconds} ms.",
            catalog.Models.Count,
            stopwatch.ElapsedMilliseconds);

        return catalog;
    }
}
