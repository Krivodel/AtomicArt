using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionExistingAnimatorTests : GalleryMotionAnimatorTestBase
{
    [Fact]
    public async Task AnimateExistingAsync_WithMovedCard_UsesReferenceDelayDurationAndEase()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        Guid itemId = Guid.NewGuid();
        double pitch = GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap;
        int delayMilliseconds = 80;
        int durationMilliseconds = 625;
        Border control = CreatePositionedControl(pitch, 0d);
        context.CardControls[itemId] = control;
        Dictionary<Guid, Rect> firstSnapshot = [];
        firstSnapshot[itemId] = new Rect(0d, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight);
        GalleryAnimationTracker tracker = [];

        Task animationTask = animator.AnimateFrontMaterializationAsync(
            context,
            firstSnapshot,
            new HashSet<Guid>(),
            tracker);

        appliedFrames.Should().ContainSingle();
        appliedFrames[0].Frame.Should().Be(new MotionFrame(-pitch, 0d, 1d, 0d, 1d));
        tracker.Should().ContainSingle().Which.Should().BeSameAs(control);

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(delayMilliseconds - 1d));

        appliedFrames.Last().Frame.Should().Be(new MotionFrame(-pitch, 0d, 1d, 0d, 1d));

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(delayMilliseconds + (durationMilliseconds / 2d)));

        List<MotionFrame> referenceFrames = GalleryMotionPlanner.BuildExistingFrames(
            firstSnapshot[itemId],
            new Rect(pitch, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            0,
            0,
            context.OverlayCanvas.Bounds);
        MotionFrame expectedMiddleFrame = Interpolate(referenceFrames, MotionEasing.EaseRail(0.5d));
        appliedFrames.Last().Frame.Should().Be(expectedMiddleFrame);

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(delayMilliseconds + durationMilliseconds));
        await animationTask;

        appliedFrames.Last().Frame.Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }
}
