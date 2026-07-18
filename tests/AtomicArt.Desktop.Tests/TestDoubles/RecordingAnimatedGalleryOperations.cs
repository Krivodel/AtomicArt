using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingAnimatedGalleryOperations : IAnimatedGalleryOperations
{
    public int AppendBatchCallCount { get; private set; }
    public int GenerateFrontCallCount { get; private set; }
    public int RemoveCallCount { get; private set; }
    public int MixedMutationCallCount { get; private set; }
    public int RestoreSnapshotCallCount { get; private set; }
    public IReadOnlyList<object> LastAppendItems { get; private set; } = [];
    public IReadOnlyList<object> LastGenerateFrontItems { get; private set; } = [];
    public Guid? LastRemovedItemId { get; private set; }
    public IReadOnlyList<object> LastMixedMutationItems { get; private set; } = [];
    public IReadOnlyList<object> LastRestoreSnapshotItems { get; private set; } = [];

    public Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();

        AppendBatchCallCount++;
        LastAppendItems = items.ToArray();

        return Task.CompletedTask;
    }

    public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();

        GenerateFrontCallCount++;
        LastGenerateFrontItems = items.ToArray();

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        RemoveCallCount++;
        LastRemovedItemId = itemId;

        return Task.CompletedTask;
    }

    public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);
        ct.ThrowIfCancellationRequested();

        MixedMutationCallCount++;
        LastMixedMutationItems = finalItems.ToArray();

        return Task.CompletedTask;
    }

    public Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);
        ct.ThrowIfCancellationRequested();

        RestoreSnapshotCallCount++;
        LastRestoreSnapshotItems = finalItems.ToArray();

        return Task.CompletedTask;
    }
}
