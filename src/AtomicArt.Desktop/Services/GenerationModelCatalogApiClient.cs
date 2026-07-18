using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class GenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IApiEndpointService _apiEndpointService;
    private readonly ILogger<GenerationModelCatalogApiClient> _logger;

    public GenerationModelCatalogApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger<GenerationModelCatalogApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _apiEndpointService = apiEndpointService;
        _logger = logger;
    }

    public async Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Loading generation model catalog from Atomic Art API.");

        Uri requestUri = _apiEndpointService.CreateRequestUri(GenerationApiRoutes.Models);
        using HttpResponseMessage response = await _httpClient
            .GetAsync(requestUri, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            SafeApiProblemDetailsReadResult problemDetails = await SafeApiProblemDetailsReader
                .TryReadErrorCodeAsync(response.Content, ct)
                .ConfigureAwait(false);
            SafeApiProblemDetailsReader.LogReadFailure(
                _logger,
                problemDetails,
                SafeApiProblemDetailsApi.GenerationModelCatalog);
            _logger.LogWarning(
                "Generation model catalog API returned HTTP {StatusCode} with error code {ErrorCode} after {ElapsedMilliseconds} ms.",
                (int)response.StatusCode,
                problemDetails.LogErrorCode,
                stopwatch.ElapsedMilliseconds);
        }

        response.EnsureSuccessStatusCode();

        GenerationModelCatalogDto? catalog = await response.Content
            .ReadFromJsonAsync<GenerationModelCatalogDto>(ct)
            .ConfigureAwait(false);

        if (catalog is null)
        {
            throw new InvalidOperationException("Generation model catalog API returned an empty response.");
        }

        _logger.LogInformation(
            "Generation model catalog loaded with {ModelCount} models after {ElapsedMilliseconds} ms.",
            catalog.Models.Count,
            stopwatch.ElapsedMilliseconds);

        return catalog;
    }
}
