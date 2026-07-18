using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMixedMutationRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenMultipleMixedMutationsAreBatched_UsesLastFinalItems()
    {
        DiscardingUiFrameScheduler frameScheduler = new();
        GalleryAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryLayoutService layout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryMixedMutationRunner runner = new(animator, layout, NullLogger<GalleryMixedMutationRunner>.Instance);
        List<object> items = [];
        GalleryOperationCoordinator context = CreateContext(frameScheduler, items);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid thirdId = Guid.NewGuid();
        GalleryOperation firstOperation = new MixedMutationGalleryOperation(new List<object> { firstId });
        GalleryOperation secondOperation = new MixedMutationGalleryOperation(new List<object> { secondId, thirdId });

        await runner.RunAsync(
            new List<GalleryOperation> { firstOperation, secondOperation },
            context,
            CancellationToken.None);

        items.Should().Equal(secondId, thirdId);
        firstOperation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();
        secondOperation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static GalleryOperationCoordinator CreateContext(
        IUiFrameScheduler frameScheduler,
        IList<object> items)
    {
        return GalleryOperationCoordinatorTestFactory.CreateAttached(
            frameScheduler,
            items);
    }
}
