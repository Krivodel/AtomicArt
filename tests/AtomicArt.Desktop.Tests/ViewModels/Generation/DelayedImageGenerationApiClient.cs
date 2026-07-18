using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

internal sealed class DelayedImageGenerationApiClient : IImageGenerationApiClient
{
    public Task RequestReceivedTask => _requestReceived.Task;
    public int RequestCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _requestCount;
            }
        }
    }

    private static readonly DateTime CreatedAtUtc = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
    private readonly object _syncRoot = new();
    private readonly TaskCompletionSource<ImageGenerationRequestDto> _requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<GenerationBatchDto> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _requestCount;
    private string? _providerCredential;

    public async Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            _requestCount++;
            _providerCredential = providerCredential;
        }

        _requestReceived.TrySetResult(request);

        await _completion.Task.WaitAsync(ct);

        return CreateBatch(request);
    }

    public void Complete()
    {
        if (RequestCount == 0)
        {
            throw new InvalidOperationException("Generation request was not received.");
        }

        if (!HasProviderCredential())
        {
            throw new InvalidOperationException("Provider credential was not received.");
        }

        _completion.TrySetResult(CreateBatch(CreateDefaultRequest()));
    }

    private static ImageGenerationRequestDto CreateDefaultRequest()
    {
        return new ImageGenerationRequestDto(
            "test-model",
            "Prompt",
            "1:1",
            "1k",
            1d,
            1,
            []);
    }

    private bool HasProviderCredential()
    {
        lock (_syncRoot)
        {
            return !string.IsNullOrWhiteSpace(_providerCredential);
        }
    }

    private static GenerationBatchDto CreateBatch(ImageGenerationRequestDto request)
    {
        List<GenerationItemDto> items =
        [
            new(
                Guid.NewGuid(),
                request.ModelId,
                "Nano Banana 2",
                request.Prompt,
                request.AspectRatio,
                request.Resolution,
                CreatedAtUtc,
                GenerationItemStatus.Generated,
                null,
                null)
        ];

        return new GenerationBatchDto(Guid.NewGuid(), items);
    }
}
