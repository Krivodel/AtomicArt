namespace AtomicArt.Desktop.Services.Generation;

internal sealed class NullGenerationCancellationService
    : IGenerationCancellationService
{
    public static NullGenerationCancellationService Instance { get; } = new();

    private NullGenerationCancellationService()
    {
    }

    public void Register(Guid logicalGenerationId, Action cancel)
    {
    }

    public void Unregister(Guid logicalGenerationId)
    {
    }

    public void Cancel(Guid logicalGenerationId)
    {
    }
}
