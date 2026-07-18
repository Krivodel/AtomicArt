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
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    void IAnimatedGalleryOperationsRegistration.Attach(IAnimatedGalleryOperations operations)
    {
        _attachedOperations = operations;
    }

    void IAnimatedGalleryOperationsRegistration.Detach(IAnimatedGalleryOperations operations)
    {
        _detachedOperations = operations;
    }
}
