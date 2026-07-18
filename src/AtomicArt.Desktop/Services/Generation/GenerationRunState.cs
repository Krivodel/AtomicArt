namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationRunState
{
    public bool IsStarted { get; private set; }

    public void MarkStarted()
    {
        IsStarted = true;
    }
}
