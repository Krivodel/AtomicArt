namespace AtomicArt.Desktop.Views.Gallery;

public interface IGenerationPreviewSession : IDisposable
{
    void Attach(GenerationPreviewControl previewControl);
}
