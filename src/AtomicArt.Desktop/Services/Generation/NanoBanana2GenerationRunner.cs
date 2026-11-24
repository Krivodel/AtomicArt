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
                GenerationRequestValidationMessages.PositiveGenerationCountRequired);
        }

        NanoBanana2GenerationParameters singleGenerationParameters = parameters with
        {
            GenerationCount = 1
        };

        int generationIndex = 0;

        try
        {
            for (; generationIndex < parameters.GenerationCount; generationIndex++)
            {
                ImageGenerationRequestDto request = _requestBuilder.CreateValidatedRequest(singleGenerationParameters);
                GenerationStartSnapshot startSnapshot = _requestBuilder.CreateStartSnapshot(
                    request,
                    singleGenerationParameters) with
                { ChatHistory = parameters.ChatBatch?.History };
                GenerationRunRequest runRequest = new(request, startSnapshot, providerCredential,
                    singleGenerationParameters.CodexTurnOptions)
                {
                    ChatRun = parameters.ChatBatch?.Runs[generationIndex],
                    ChatReferences = parameters.ChatBatch?.UserMessage.Images
                };

                await _runDispatcher.EnqueueAsync(runRequest, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (parameters.ChatBatch is { } chat)
            {
                foreach (GenerationChatRun run in chat.Runs.Skip(generationIndex))
                {
                    run.Complete(null);
                }
            }
        }
    }
}
