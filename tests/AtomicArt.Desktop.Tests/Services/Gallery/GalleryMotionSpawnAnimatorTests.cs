using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionSpawnAnimatorTests : GalleryMotionAnimatorTestBase
{
    [Fact]
    public void AnimateSpawnRetargetAsync_WhenTargetsProvided_CreatesTemporaryCopiesAndReferenceFirstFrames()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        double pitch = GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap;
        Border firstControl = CreatePositionedControl(0d, 0d);
        Border secondControl = CreatePositionedControl(pitch, 0d);
        context.CardControls[firstId] = firstControl;
        context.CardControls[secondId] = secondControl;
        GalleryFrontGenerationRunState state = CreateFrontState(new List<object> { firstId, secondId });
        double expectedStartX = pitch / 2d;
        double expectedStartY = (GalleryLayoutService.CardHeight * 0.42d)
            - (GalleryLayoutService.CardHeight / 2d);

        Task animationTask = animator.AnimateSpawnRetargetAsync(
            context,
            new Dictionary<Guid, Rect>(),
            state);

        animationTask.IsCompleted.Should().BeFalse();
        state.SpawnClones.Should().HaveCount(2);
        state.RunningControls.Should().HaveCount(6);
        state.OverlayControls.Should().HaveCount(6);
        context.OverlayCanvas.Children.Should().HaveCount(6);
        appliedFrames.Should().HaveCount(6);
        appliedFrames[3].Control.Should().BeSameAs(state.SpawnClones[firstId]);
        appliedFrames[3].Frame.X.Should().BeApproximately(expectedStartX, 0.000000000001d);
        appliedFrames[3].Frame.Y.Should().BeApproximately(expectedStartY, 0.000000000001d);
        appliedFrames[3].Frame.Scale.Should().Be(0.30d);
        appliedFrames[3].Frame.Opacity.Should().Be(0d);
        appliedFrames[5].Control.Should().BeSameAs(state.SpawnClones[secondId]);
        appliedFrames[5].Frame.X.Should().BeApproximately(-expectedStartX, 0.000000000001d);
        appliedFrames[5].Frame.Y.Should().BeApproximately(expectedStartY, 0.000000000001d);
        appliedFrames[5].Frame.Scale.Should().Be(0.30d);
        appliedFrames[5].Frame.Opacity.Should().Be(0d);
    }

    [Fact]
    public async Task AnimateSpawnRetargetAsync_WithEightItems_WaitsForLastTargetFlash()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        List<object> items = CreatePositionedItems(context, 8);
        GalleryFrontGenerationRunState state = CreateFrontState(items);

        Task animationTask = animator.AnimateSpawnRetargetAsync(
            context,
            new Dictionary<Guid, Rect>(),
            state);

        context.OverlayCanvas.Children.Should().HaveCount(18);
        state.OverlayControls.Should().HaveCount(18);

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(1100d));

        animationTask.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(1146d));
        await animationTask;

        animationTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void AnimateSpawnRetargetAsync_WithNineItems_CreatesTargetFlashOnlyThroughIndexSeven()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        List<object> items = CreatePositionedItems(context, 9);
        GalleryFrontGenerationRunState state = CreateFrontState(items);

        Task animationTask = animator.AnimateSpawnRetargetAsync(
            context,
            new Dictionary<Guid, Rect>(),
            state);

        animationTask.IsCompleted.Should().BeFalse();
        state.SpawnClones.Should().HaveCount(9);
        context.OverlayCanvas.Children.Should().HaveCount(19);
        state.OverlayControls.Should().HaveCount(19);
    }

    [Fact]
    public async Task AnimateSpawnRetargetAsync_WithCurrentSpawnRect_UsesRetargetTimingAndStartState()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        Guid itemId = Guid.NewGuid();
        double pitch = GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap;
        Border control = CreatePositionedControl(pitch, 0d);
        context.CardControls[itemId] = control;
        Dictionary<Guid, Rect> currentSpawnRects = [];
        currentSpawnRects[itemId] = new Rect(20d, 40d, 75d, 119d);
        GalleryFrontGenerationRunState state = CreateFrontState(new List<object> { itemId });

        Task animationTask = animator.AnimateSpawnRetargetAsync(
            context,
            currentSpawnRects,
            state);

        appliedFrames.Should().ContainSingle();
        appliedFrames[0].Frame.X.Should().BeApproximately(-296.5d, 0.000000000001d);
        appliedFrames[0].Frame.Y.Should().BeApproximately(-69.5d, 0.000000000001d);
        appliedFrames[0].Frame.Scale.Should().BeApproximately(75d / GalleryLayoutService.CardWidth, 0.000000000001d);
        appliedFrames[0].Frame.Opacity.Should().Be(1d);

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(759d));

        animationTask.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(760d));
        await animationTask;

        animationTask.IsCompletedSuccessfully.Should().BeTrue();
    }
}
