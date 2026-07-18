using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

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
}
