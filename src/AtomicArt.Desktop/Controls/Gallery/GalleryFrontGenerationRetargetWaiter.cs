using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryFrontGenerationRetargetWaiter
{
    private readonly GalleryAnimationScheduler _animationScheduler;
    private TaskCompletionSource? _retargetRequested;
    private bool _retargetPending;

    public GalleryFrontGenerationRetargetWaiter(GalleryAnimationScheduler animationScheduler)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }

    internal void RequestRetarget()
    {
        if (_retargetRequested is null)
        {
            _retargetPending = true;
            return;
        }

        _retargetRequested.TrySetResult();
    }

    internal void Reset()
    {
        _retargetRequested = null;
        _retargetPending = false;
    }

    internal async Task<FrontGenerationCycleResult> WaitForCycleAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        Task animationTask,
        CancellationToken ct)
    {
        Task cancellationTask = CreateCancellationTask(ct, out CancellationTokenRegistration cancellationRegistration);
        await using (cancellationRegistration)
        {
            PrepareRetargetWait(context);
            return await WaitForCycleCompletionAsync(context, state, animationTask, cancellationTask, ct);
        }
    }

    private static Task CreateCancellationTask(
        CancellationToken ct,
        out CancellationTokenRegistration cancellationRegistration)
    {
        TaskCompletionSource cancellationRequested = new();
        cancellationRegistration = ct.Register(() => cancellationRequested.TrySetResult());

        return cancellationRequested.Task;
    }

    private static async Task<FrontGenerationCycleResult> CompleteAnimationCycleAsync(
        GalleryOperationCoordinator context,
        Task animationTask)
    {
        await animationTask;
        List<GalleryOperation> nextOperations = context.DrainLeadingOperations(typeof(GenerateFrontGalleryOperation));

        return new FrontGenerationCycleResult(nextOperations.Count > 0, nextOperations);
    }

    private static async Task<FrontGenerationCycleResult> CompleteRetargetCycleAsync(
        GalleryOperationCoordinator context,
        Task animationTask)
    {
        List<GalleryOperation> retargetOperations = context.DrainLeadingOperations(typeof(GenerateFrontGalleryOperation));
        if (retargetOperations.Count == 0)
        {
            await animationTask;
            return new FrontGenerationCycleResult(false, retargetOperations);
        }

        return new FrontGenerationCycleResult(true, retargetOperations);
    }

    private async Task<FrontGenerationCycleResult> WaitForCycleCompletionAsync(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        Task animationTask,
        Task cancellationTask,
        CancellationToken ct)
    {
        Task retargetTask = _retargetRequested?.Task
                            ?? throw new InvalidOperationException("Gallery retarget waiter was not created.");
        Task finished = await Task.WhenAny(animationTask, retargetTask, cancellationTask);
        if (finished == cancellationTask)
        {
            CancelCycle(context, state, ct);
        }

        return finished == animationTask
            ? await CompleteAnimationCycleAsync(context, animationTask)
            : await CompleteRetargetCycleAsync(context, animationTask);
    }

    private void PrepareRetargetWait(GalleryOperationCoordinator context)
    {
        _retargetRequested = new TaskCompletionSource();
        if (!_retargetPending && !context.HasLeadingOperation(typeof(GenerateFrontGalleryOperation)))
        {
            return;
        }

        _retargetPending = false;
        _retargetRequested.TrySetResult();
    }

    private void CancelCycle(
        GalleryOperationCoordinator context,
        GalleryFrontGenerationRunState state,
        CancellationToken ct)
    {
        _animationScheduler.Cancel(state.RunningControls);
        state.RemoveOverlays(context.OverlayCanvas);
        ct.ThrowIfCancellationRequested();
    }
}
