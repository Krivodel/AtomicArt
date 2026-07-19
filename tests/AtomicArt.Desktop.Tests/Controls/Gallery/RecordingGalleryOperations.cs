using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

internal sealed class RecordingGalleryOperations : IAnimatedGalleryOperations, IAnimatedGalleryOperationsRegistration
{
    public IAnimatedGalleryOperations? AttachedOperations => _attachedOperations;
    public IAnimatedGalleryOperations? DetachedOperations => _detachedOperations;

    private IAnimatedGalleryOperations? _attachedOperations;
    private IAnimatedGalleryOperations? _detachedOperations;

    public Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        return CompleteCollectionOperation(items, ct);
    }

    public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        return CompleteCollectionOperation(items, ct);
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        return CompleteOperation(ct);
    }

    public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        return CompleteCollectionOperation(finalItems, ct);
    }

    public Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        return CompleteCollectionOperation(finalItems, ct);
    }

    void IAnimatedGalleryOperationsRegistration.Attach(IAnimatedGalleryOperations operations)
    {
        _attachedOperations = operations;
    }

    void IAnimatedGalleryOperationsRegistration.Detach(IAnimatedGalleryOperations operations)
    {
        _detachedOperations = operations;
    }

    private static Task CompleteCollectionOperation(
        IReadOnlyList<object> items,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        return CompleteOperation(ct);
    }

    private static Task CompleteOperation(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
