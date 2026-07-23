namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationCancellationService
    : IGenerationCancellationService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, Action> _cancellations = [];

    public void Register(Guid logicalGenerationId, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        if (logicalGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Logical generation identifier must not be empty.",
                nameof(logicalGenerationId));
        }

        lock (_syncRoot)
        {
            if (!_cancellations.TryAdd(logicalGenerationId, cancel))
            {
                throw new InvalidOperationException(
                    "Logical generation cancellation is already registered.");
            }
        }
    }

    public void Unregister(Guid logicalGenerationId)
    {
        lock (_syncRoot)
        {
            _cancellations.Remove(logicalGenerationId);
        }
    }

    public void Cancel(Guid logicalGenerationId)
    {
        Action? cancel;

        lock (_syncRoot)
        {
            _cancellations.TryGetValue(logicalGenerationId, out cancel);
        }

        cancel?.Invoke();
    }
}
