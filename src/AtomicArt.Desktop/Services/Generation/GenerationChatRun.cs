namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationChatRun
{
    public Guid Id { get; } = Guid.NewGuid();
    public Task<GenerationChatResult?> Completion => _completion.Task;

    private readonly TaskCompletionSource<GenerationChatResult?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Complete(GenerationChatResult? result)
    {
        _completion.TrySetResult(result);
    }
}
