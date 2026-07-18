using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class NanoBanana2GenerationRunner : INanoBanana2GenerationRunner, IGenerationModelService
{
    private readonly NanoBanana2GenerationRequestBuilder _requestBuilder;
    private readonly IGenerationRunDispatcher _runDispatcher;

    public NanoBanana2GenerationRunner(
        NanoBanana2GenerationRequestBuilder requestBuilder,
        IGenerationRunDispatcher runDispatcher)
    {
        ArgumentNullException.ThrowIfNull(requestBuilder);
        ArgumentNullException.ThrowIfNull(runDispatcher);

        _requestBuilder = requestBuilder;
        _runDispatcher = runDispatcher;
    }

    public async Task RunAsync(
        NanoBanana2GenerationParameters parameters,
        string providerCredential,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(providerCredential);

        if (parameters.GenerationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters.GenerationCount),
                "Generation count must be positive.");
        }

        NanoBanana2GenerationParameters singleGenerationParameters = parameters with
        {
            GenerationCount = 1
        };

        for (int generationIndex = 0; generationIndex < parameters.GenerationCount; generationIndex++)
        {
            ImageGenerationRequestDto request = _requestBuilder.CreateValidatedRequest(singleGenerationParameters);
            GenerationStartSnapshot startSnapshot = _requestBuilder.CreateStartSnapshot(
                request,
                singleGenerationParameters.ModelDisplayName);
            GenerationRunRequest runRequest = new(request, startSnapshot, providerCredential);

            await _runDispatcher
                .EnqueueAsync(runRequest, ct)
                .ConfigureAwait(false);
        }
    }
}
