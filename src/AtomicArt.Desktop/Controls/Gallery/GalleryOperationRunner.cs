using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryOperationRunner : IGalleryOperationRunner
{
    public abstract Type OperationType { get; }
    public abstract bool SupportsBatching { get; }

    public bool CanRun(IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return CanRunCore(operations);
    }

    public IReadOnlyList<GalleryOperation> SelectOperations(
        IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return SelectOperationsCore(operations);
    }

    public Task RunAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(context);

        return RunCoreAsync(operations, context, ct);
    }

    protected virtual bool CanRunCore(IReadOnlyList<GalleryOperation> operations)
    {
        return GalleryOperationTypeSelector.Contains(operations, OperationType);
    }

    protected virtual IReadOnlyList<GalleryOperation> SelectOperationsCore(
        IReadOnlyList<GalleryOperation> operations)
    {
        return GalleryOperationTypeSelector.Select(operations, OperationType);
    }

    protected abstract Task RunCoreAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct);
}
