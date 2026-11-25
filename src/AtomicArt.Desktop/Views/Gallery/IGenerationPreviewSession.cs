namespace AtomicArt.Desktop.Views.Gallery;

public interface IGenerationPreviewSession : IDisposable
{
    void Attach(GenerationPreviewControl previewControl);
    void Detach(GenerationPreviewControl previewControl);
}
