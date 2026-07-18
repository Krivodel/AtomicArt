namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryOperationRunnerRegistry
{
    IReadOnlyList<IGalleryOperationRunner> Runners { get; }

    IGalleryOperationRunner GetRunner(Type operationType);
}
