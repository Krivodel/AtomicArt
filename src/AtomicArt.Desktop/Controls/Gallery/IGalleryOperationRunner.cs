using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryOperationRunner
{
    Type OperationType { get; }

    bool SupportsBatching { get; }

    bool CanRun(IReadOnlyList<GalleryOperation> operations);

    IReadOnlyList<GalleryOperation> SelectOperations(IReadOnlyList<GalleryOperation> operations);

    Task RunAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct);
}

internal interface IGalleryRetargetableOperationRunner : IGalleryOperationRunner
{
    bool IsRunning { get; }
    void RequestRetarget();
}
