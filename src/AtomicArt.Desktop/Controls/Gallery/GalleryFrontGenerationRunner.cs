using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryFrontGenerationRunner :
    GalleryAnimatedOperationRunner,
    IGalleryRetargetableOperationRunner
{
    public override Type OperationType => typeof(GenerateFrontGalleryOperation);
    public override bool SupportsBatching => true;
    public bool IsRunning => _isRunning;

    private readonly GalleryAnimationScheduler _animationScheduler;
    private readonly GalleryFrontGenerationRetargetWaiter _retargetWaiter;
    private bool _isRunning;

    public GalleryFrontGenerationRunner(
        GalleryAnimationScheduler animationScheduler,
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryFrontGenerationRunner> logger,
        GalleryFrontGenerationRetargetWaiter retargetWaiter)
        : base(motionAnimator, galleryLayout, logger)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
        _retargetWaiter = retargetWaiter ?? throw new ArgumentNullException(nameof(retargetWaiter));
    }

    public void RequestRetarget()
    {
        _retargetWaiter.RequestRetarget();
    }

    protected override async Task RunCoreAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        GalleryFrontGenerationRunState state = new(operations);

        try
        {
            BeginRun(context, ct);
            await RunLoopAsync(context, state, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            GalleryOperationCompletion.Cancel(GetTrackedOperations(state, operations), ct);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to generate front gallery items.");
            GalleryOperationCompletion.Fail(GetTrackedOperations(state, operations), exception);
        }
        finally
        {
            FinishRun(context, state);
        }
    }

    private static IReadOnlyList<GalleryOperation> GetTrackedOperations(
        GalleryFrontGenerationRunState state,
        IReadOnlyList<GalleryOperation> fallbackOperations)
    {
        return state.AllOperations.Count > 0
            ? state.AllOperations
            : fallbackOperations;
    }

    private static void RevealFrontItems(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state)
    {
        foreach (Guid id in state.ActiveFrontIds)
        {
            context.HiddenItemIds.Remove(id);
        }
    }

    private void RefreshRevealedFrontItems(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state)
    {
        if (state.ActiveFrontIds.Count == 0)
        {
            return;
        }

        if (context.ScrollViewer.Dispatcher.CheckAccess())
        {
            GalleryLayout.RefreshGalleryVirtualization(context);
            return;
        }

        context.ScrollViewer.Dispatcher.Post(() => GalleryLayout.RefreshGalleryVirtualization(context));
    }

    private async Task RunLoopAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        CancellationToken ct)
    {
        while (true)
        {
            FrontGenerationCycleResult result = await RunCycleAsync(context, state, ct);
            if (!result.ShouldRetarget)
            {
                break;
            }

            state.UnmaterializedOperations.AddRange(result.NextOperations);
            _animationScheduler.Cancel(state.RunningControls);
        }

        state.RemoveOverlays(context.OverlayCanvas);
        GalleryOperationCompletion.Complete(state.AllOperations);
    }

    private async Task<FrontGenerationCycleResult> RunCycleAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool startedEmpty = context.Items.Count == 0;
        if (!startedEmpty)
        {
            await PrepareVisibleCardsForSnapshotAsync(context);
        }

        GalleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> firstExisting = GalleryLayout.TakeSnapshotExcept(context, state.ActiveFrontIds);
        Dictionary<Guid, Rect> currentSpawnRects =
            GalleryLayout.TakeOverlaySnapshots(context.OverlayCanvas, state.SpawnClones);
        MaterializeOperations(context, state);
        await PrepareMaterializedEmptySceneAsync(context, state, startedEmpty);

        Task animationTask = await CreateAnimationTaskAsync(
            context,
            state,
            firstExisting,
            currentSpawnRects);
        (bool shouldRetarget, List<GalleryOperation> nextOperations) =
            await _retargetWaiter.WaitForCycleAsync(context, state, animationTask, ct);

        return new FrontGenerationCycleResult(shouldRetarget, nextOperations);
    }

    private async Task PrepareVisibleCardsForSnapshotAsync(GalleryOperationCoordinator context)
    {
        GalleryLayout.RefreshGalleryVirtualization(context);
        await context.WaitForLayoutAsync();
    }

    private async Task PrepareMaterializedEmptySceneAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        bool startedEmpty)
    {
        if (!startedEmpty || (state.ActiveFrontItems.Count == 0))
        {
            return;
        }

        GalleryLayout.RefreshGalleryVirtualization(context);
        await context.WaitForLayoutAsync();
    }

    private async Task<Task> CreateAnimationTaskAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        Dictionary<Guid, Rect> firstExisting,
        Dictionary<Guid, Rect> currentSpawnRects)
    {
        GalleryLayout.RenderCards(context, state.ActiveFrontIds);
        await context.WaitForLayoutAsync();

        state.RunningControls.Clear();
        List<Task> animations =
        [
            MotionAnimator.AnimateFrontMaterializationAsync(
                context,
                firstExisting,
                state.ActiveFrontIds,
                state.RunningControls),
            MotionAnimator.AnimateSpawnRetargetAsync(
                context,
                currentSpawnRects,
                state)
        ];

        return Task.WhenAll(animations);
    }

    private void BeginRun(GalleryOperationCoordinator context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _isRunning = true;
        context.ScrollViewer.Offset = new Vector(0d, 0d);
    }

    private void FinishRun(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state)
    {
        _animationScheduler.Cancel(state.RunningControls);
        RevealFrontItems(context, state);
        state.RemoveOverlays(context.OverlayCanvas);
        RefreshRevealedFrontItems(context, state);
        _retargetWaiter.Reset();
        _isRunning = false;
        context.NotifyStateChanged();
    }

    private void MaterializeOperations(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state)
    {
        state.RemoveOverlays(context.OverlayCanvas);

        foreach (GalleryOperation operation in state.UnmaterializedOperations)
        {
            List<object> batch = operation.Items.ToList();
            state.AllOperations.Add(operation);
            if (batch.Count == 0)
            {
                continue;
            }

            state.ActiveFrontItems.InsertRange(0, batch);
            context.InsertItemsAtStart(batch);

            foreach (object item in batch)
            {
                state.ActiveFrontIds.Add(context.GetItemId(item));
            }
        }

        state.UnmaterializedOperations.Clear();
    }
}
