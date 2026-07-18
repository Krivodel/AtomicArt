namespace AtomicArt.Desktop.Services;

public interface IGenerationLifecycleEventHub
{
    IDisposable Subscribe(Action<GenerationLifecycleEvent> subscriber);

    void Publish(GenerationLifecycleEvent lifecycleEvent);
}
