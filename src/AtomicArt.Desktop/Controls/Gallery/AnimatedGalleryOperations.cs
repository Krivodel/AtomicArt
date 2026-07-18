using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGalleryOperations :
    IAnimatedGalleryOperations,
    IAnimatedGalleryOperationsRegistration,
    IAnimatedGallerySceneFactoryProvider
{
    internal IAnimatedGalleryOperations? ActiveOperations => GetActiveOperations();

    private readonly IAnimatedGallerySceneFactory _sceneFactory;
    private readonly ILogger<AnimatedGalleryOperations> _logger;
    private readonly object _syncRoot = new();
    private IAnimatedGalleryOperations? _activeOperations;

    public AnimatedGalleryOperations(
        IAnimatedGallerySceneFactory sceneFactory,
        ILogger<AnimatedGalleryOperations> logger)
    {
        _sceneFactory = sceneFactory ?? throw new ArgumentNullException(nameof(sceneFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task AppendBatchAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        IAnimatedGalleryOperations? operations = GetActiveOperations();
        if (operations is null)
        {
            LogSkippedOperation(nameof(AppendBatchAsync), items.Count);
            return Task.CompletedTask;
        }

        return operations.AppendBatchAsync(items, ct);
    }

    public Task GenerateFrontAsync(IReadOnlyList<object> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        IAnimatedGalleryOperations? operations = GetActiveOperations();
        if (operations is null)
        {
            LogSkippedOperation(nameof(GenerateFrontAsync), items.Count);
            return Task.CompletedTask;
        }

        return operations.GenerateFrontAsync(items, ct);
    }

    public Task RemoveAsync(Guid itemId, CancellationToken ct)
    {
        IAnimatedGalleryOperations? operations = GetActiveOperations();
        if (operations is null)
        {
            LogSkippedOperation(nameof(RemoveAsync), 1);
            return Task.CompletedTask;
        }

        return operations.RemoveAsync(itemId, ct);
    }

    public Task ApplyMixedMutationAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);

        IAnimatedGalleryOperations? operations = GetActiveOperations();
        if (operations is null)
        {
            LogSkippedOperation(nameof(ApplyMixedMutationAsync), finalItems.Count);
            return Task.CompletedTask;
        }

        return operations.ApplyMixedMutationAsync(finalItems, ct);
    }

    public Task RestoreSnapshotAsync(IReadOnlyList<object> finalItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(finalItems);

        IAnimatedGalleryOperations? operations = GetActiveOperations();
        if (operations is null)
        {
            LogSkippedOperation(nameof(RestoreSnapshotAsync), finalItems.Count);
            return Task.CompletedTask;
        }

        return operations.RestoreSnapshotAsync(finalItems, ct);
    }

    IAnimatedGallerySceneFactory IAnimatedGallerySceneFactoryProvider.SceneFactory => _sceneFactory;

    void IAnimatedGalleryOperationsRegistration.Attach(IAnimatedGalleryOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        lock (_syncRoot)
        {
            _activeOperations = operations;
        }

        _logger.LogDebug("Attached active animated gallery scene operations");
    }

    void IAnimatedGalleryOperationsRegistration.Detach(IAnimatedGalleryOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        bool detached = false;

        lock (_syncRoot)
        {
            if (ReferenceEquals(_activeOperations, operations))
            {
                _activeOperations = null;
                detached = true;
            }
        }

        if (detached)
        {
            _logger.LogDebug("Detached active animated gallery scene operations");
        }
    }

    private IAnimatedGalleryOperations? GetActiveOperations()
    {
        lock (_syncRoot)
        {
            return _activeOperations;
        }
    }

    private void LogSkippedOperation(string operationName, int itemCount)
    {
        _logger.LogDebug(
            "Skipped gallery operation {OperationName} for {ItemCount} items because no scene is attached",
            operationName,
            itemCount);
    }
}
