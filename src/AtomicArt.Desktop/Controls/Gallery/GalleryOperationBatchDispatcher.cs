using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryOperationBatchDispatcher
{
    private readonly IGalleryOperationRunnerRegistry _runnerRegistry;

    public GalleryOperationBatchDispatcher(IGalleryOperationRunnerRegistry runnerRegistry)
    {
        _runnerRegistry = runnerRegistry ?? throw new ArgumentNullException(nameof(runnerRegistry));
    }

    internal async Task DispatchAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        CancellationToken ct)
    {
        for (int i = 0; i < operations.Count;)
        {
            ct.ThrowIfCancellationRequested();
            if (IsBatchableOperation(operations[i]))
            {
                i = await DispatchBatchableAddOperationsAsync(context, operations, i, ct);
                continue;
            }

            i = await DispatchMutationOperationsAsync(context, operations, i, ct);
        }
    }

    private async Task<int> DispatchBatchableAddOperationsAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        int startIndex,
        CancellationToken ct)
    {
        List<GalleryOperation> addBatch = [];
        int index = startIndex;
        while ((index < operations.Count) && IsBatchableOperation(operations[index]))
        {
            addBatch.Add(operations[index]);
            index++;
        }

        await RunMatchingRunnersAsync(context, addBatch, ct);

        return index;
    }

    private async Task<int> DispatchMutationOperationsAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        int startIndex,
        CancellationToken ct)
    {
        List<GalleryOperation> mutationBatch = [];
        int index = startIndex;
        while ((index < operations.Count) && !IsBatchableOperation(operations[index]))
        {
            mutationBatch.Add(operations[index]);
            index++;
        }

        if (mutationBatch.Count == 0)
        {
            return index;
        }

        await RunMatchingRunnersAsync(context, mutationBatch, ct);

        return index;
    }

    private async Task RunMatchingRunnersAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        CancellationToken ct)
    {
        foreach (IGalleryOperationRunner runner in _runnerRegistry.Runners)
        {
            if (!runner.CanRun(operations))
            {
                continue;
            }

            await runner.RunAsync(runner.SelectOperations(operations), context, ct);
        }
    }

    private bool IsBatchableOperation(GalleryOperation operation)
    {
        return _runnerRegistry
            .Runners
            .Any(runner => runner.SupportsBatching
                && GalleryOperationTypeSelector.Matches(operation, runner.OperationType));
    }
}
