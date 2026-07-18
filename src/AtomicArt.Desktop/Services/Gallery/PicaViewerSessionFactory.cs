namespace AtomicArt.Desktop.Services.Gallery;

public sealed class PicaViewerSessionFactory
{
    private readonly PicaViewerSessionDependencies _dependencies;

    public PicaViewerSessionFactory(PicaViewerSessionDependencies dependencies)
    {
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
    }

    internal PicaViewerSession Create()
    {
        return new PicaViewerSession(_dependencies);
    }
}
