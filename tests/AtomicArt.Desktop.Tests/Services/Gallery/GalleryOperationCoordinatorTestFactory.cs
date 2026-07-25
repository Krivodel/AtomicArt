using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal static class GalleryOperationCoordinatorTestFactory
{
    internal static GalleryOperationCoordinator Create(
        IUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry runnerRegistry)
    {
        GalleryOperationBatchDispatcher batchDispatcher = new(runnerRegistry);
        GalleryOperationQueueProcessor operationQueue = new(
            frameScheduler,
            runnerRegistry,
            batchDispatcher,
            NullLogger<GalleryOperationQueueProcessor>.Instance);

        return new GalleryOperationCoordinator(
            frameScheduler,
            runnerRegistry,
            new GalleryLayoutService(),
            operationQueue);
    }

    internal static GalleryOperationCoordinator CreateAttached(
        IUiFrameScheduler frameScheduler,
        IList<object> items,
        Func<object, Control>? controlFactory = null)
    {
        List<IGalleryOperationRunner> runners = [];
        IGalleryOperationRunnerRegistry runnerRegistry =
            new GalleryOperationRunnerRegistry(runners);

        return CreateAttached(
            frameScheduler,
            runnerRegistry,
            items,
            controlFactory);
    }

    internal static GalleryOperationCoordinator CreateAttached(
        IUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry runnerRegistry,
        IList<object> items,
        Func<object, Control>? controlFactory = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        GalleryOperationCoordinator context = Create(frameScheduler, runnerRegistry);
        context.AttachScene(
            new ScrollViewer(),
            new Canvas(),
            new Canvas(),
            items,
            item => (Guid)item,
            controlFactory ?? (_ => new Border()),
            () => Task.CompletedTask);

        return context;
    }
}
