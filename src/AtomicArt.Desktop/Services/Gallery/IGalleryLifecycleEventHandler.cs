namespace AtomicArt.Desktop.Services.Gallery;

public interface IGalleryLifecycleEventHandler
{
    GenerationLifecycleStatus Status { get; }

    Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct);
}
