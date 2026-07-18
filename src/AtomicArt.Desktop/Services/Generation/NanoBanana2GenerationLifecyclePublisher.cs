using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

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

    public void PublishCompleted(Guid correlationId, GenerationBatchDto batch)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.Completed, null, batch, null);
    }

    public void PublishStartFailed(Guid correlationId)
    {
        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.StartFailed, null, null, null);
    }

    public void PublishFailed(Guid correlationId, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        PublishLifecycleEvent(correlationId, GenerationLifecycleStatus.Failed, null, null, errorMessage);
    }

    internal void PublishCanceledGeneration(Guid correlationId, GenerationRunState runState)
    {
        if (runState.IsStarted)
        {
            PublishGenerationFailure(correlationId, runState, UiStrings.GenerationFailed);
        }
    }

    internal void PublishFailedGeneration(
        Guid correlationId,
        GenerationRunState runState,
        string errorMessage)
    {
        PublishGenerationFailure(correlationId, runState, errorMessage);
    }

    private void PublishGenerationFailure(
        Guid correlationId,
        GenerationRunState runState,
        string errorMessage)
    {
        GenerationLifecycleStatus status = runState.IsStarted
            ? GenerationLifecycleStatus.Failed
            : GenerationLifecycleStatus.StartFailed;

        PublishLifecycleEvent(correlationId, status, null, null, errorMessage);
    }

    private void PublishLifecycleEvent(
        Guid correlationId,
        GenerationLifecycleStatus status,
        GenerationStartSnapshot? start,
        GenerationBatchDto? batch,
        string? errorMessage)
    {
        GenerationLifecycleEvent lifecycleEvent = new(
            correlationId,
            status,
            start,
            batch,
            errorMessage);

        _lifecycleEventHub.Publish(lifecycleEvent);
    }
}
