using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services;

public sealed record GenerationLifecycleEvent
{
    public Guid CorrelationId { get; }
    public GenerationLifecycleStatus Status { get; }
    public GenerationStartSnapshot? Start { get; }
    public GenerationBatchDto? Batch { get; }
    public string? FailureCode { get; }
    public GenerationChatResult? ChatResult { get; init; }
    public bool IsCanceled { get; init; }

    internal GenerationChatRun? ChatRun { get; init; }

    public GenerationLifecycleEvent(
        Guid correlationId,
        GenerationLifecycleStatus status,
        GenerationStartSnapshot? start,
        GenerationBatchDto? batch,
        string? failureCode)
    {
        Validate(correlationId, status, start, batch);

        CorrelationId = correlationId;
        Status = status;
        Start = start;
        Batch = batch;
        FailureCode = failureCode;
    }

    private static void Validate(
        Guid correlationId,
        GenerationLifecycleStatus status,
        GenerationStartSnapshot? start,
        GenerationBatchDto? batch)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation id must not be empty.", nameof(correlationId));
        }

        if ((status == GenerationLifecycleStatus.Started) && (start is null))
        {
            throw new ArgumentException("Started generation lifecycle event requires start snapshot.", nameof(start));
        }

        if ((status == GenerationLifecycleStatus.Completed) && (batch is null))
        {
            throw new ArgumentException("Completed generation lifecycle event requires batch.", nameof(batch));
        }
    }
}
