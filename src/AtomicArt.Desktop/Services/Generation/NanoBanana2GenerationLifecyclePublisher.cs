using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class NanoBanana2GenerationLifecyclePublisher : IGenerationModelService
{
    private readonly IGenerationLifecycleEventHub _lifecycleEventHub;

    public NanoBanana2GenerationLifecyclePublisher(IGenerationLifecycleEventHub lifecycleEventHub)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEventHub);

        _lifecycleEventHub = lifecycleEventHub;
    }

    public void PublishStartRequested(Guid correlationId)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.StartRequested, null, null, null);
    }

    public void PublishStarted(Guid correlationId, GenerationStartSnapshot startSnapshot)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.Started, startSnapshot, null, null);
    }

    public void PublishCompleted(Guid correlationId, GenerationBatchDto batch, GenerationChatResult? chatResult = null,
        GenerationChatRun? chatRun = null)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.Completed, null, batch, null, chatResult, chatRun);
    }

    public void PublishStartFailed(Guid correlationId)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.StartFailed, null, null, null);
    }

    public void PublishFailed(Guid correlationId, string failureCode, GenerationChatResult? chatResult = null,
        GenerationChatRun? chatRun = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.Failed, null, null, failureCode, chatResult, chatRun);
    }

    internal void PublishCanceledGeneration(Guid correlationId, GenerationRunState runState)
    {
        if (runState.IsStarted)
        {
            PublishGenerationFailure(
                correlationId,
                runState,
                GenerationClientFailureCodes.Unknown);
        }
    }

    internal void PublishFailedGeneration(
        Guid correlationId,
        GenerationRunState runState,
        string failureCode)
    {
        PublishGenerationFailure(correlationId, runState, failureCode);
    }

    private void PublishGenerationFailure(
        Guid correlationId,
        GenerationRunState runState,
        string failureCode)
    {
        GenerationLifecycleStatus status = runState.IsStarted
            ? GenerationLifecycleStatus.Failed
            : GenerationLifecycleStatus.StartFailed;

        PublishLifecycleEvent(correlationId, status, null, null, failureCode);
    }

    private void PublishLifecycleEvent(
        Guid correlationId,
        GenerationLifecycleStatus status,
        GenerationStartSnapshot? start,
        GenerationBatchDto? batch,
        string? failureCode,
        GenerationChatResult? chatResult = null,
        GenerationChatRun? chatRun = null)
    {
        GenerationLifecycleEvent lifecycleEvent = new(
            correlationId,
            status,
            start,
            batch,
            failureCode)
        {
            ChatResult = chatResult,
            ChatRun = chatRun
        };

        _lifecycleEventHub.Publish(lifecycleEvent);
    }
}
