namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationChatRun
{
    public Guid Id { get; } = Guid.NewGuid();
    public Task<GenerationChatResult?> Completion => _completion.Task;
    internal int Ordinal { get; init; }

    private readonly TaskCompletionSource<GenerationChatResult?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GenerationChatRun()
    {
    }

    internal GenerationChatRun(Guid id)
    {
        Id = id;
    }

    internal void Complete(GenerationChatResult? result)
    {
        _completion.TrySetResult(result);
    }
}
