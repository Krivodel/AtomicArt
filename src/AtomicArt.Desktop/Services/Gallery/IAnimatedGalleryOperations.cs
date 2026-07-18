namespace AtomicArt.Desktop.Services.Gallery;

public interface IAnimatedGalleryOperations
{
    Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct);

    Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct);

    Task RemoveAsync(Guid itemId, CancellationToken ct);

    Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct);

    Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct);
}
