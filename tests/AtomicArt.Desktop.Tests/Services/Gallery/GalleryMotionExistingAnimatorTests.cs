using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionExistingAnimatorTests : GalleryMotionAnimatorTestBase
{
    [Fact]
    public async Task AnimateExistingAsync_WithMovedCard_UsesReferenceDelayDurationAndEase()
    {
        GalleryMotionTestScene scene = GalleryMotionTestScene.Create();
        GalleryOperationCoordinator context = scene.Context;
        Guid itemId = Guid.NewGuid();
        double pitch = GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap;
        int delayMilliseconds = 80;
        int durationMilliseconds = 625;
        Border control = CreatePositionedControl(pitch, 0d);
        context.CardControls[itemId] = control;
        Dictionary<Guid, Rect> firstSnapshot = [];
        firstSnapshot[itemId] = new Rect(0d, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight);
        GalleryAnimationTracker tracker = [];

        Task animationTask = scene.Animator.AnimateFrontMaterializationAsync(
            context,
            firstSnapshot,
            new HashSet<Guid>(),
            tracker);

        scene.AppliedFrames.Should().ContainSingle();
        scene.AppliedFrames[0].Frame.Should().Be(new MotionFrame(-pitch, 0d, 1d, 0d, 1d));
        tracker.Should().ContainSingle().Which.Should().BeSameAs(control);

        scene.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scene.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(delayMilliseconds - 1d));

        scene.AppliedFrames.Last().Frame.Should().Be(new MotionFrame(-pitch, 0d, 1d, 0d, 1d));

        scene.FrameScheduler.RunNextFrame(
            TimeSpan.FromMilliseconds(delayMilliseconds + (durationMilliseconds / 2d)));

        List<MotionFrame> referenceFrames = GalleryMotionPlanner.BuildExistingFrames(
            firstSnapshot[itemId],
            new Rect(pitch, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            0,
            0,
            context.OverlayCanvas.Bounds);
        MotionFrame expectedMiddleFrame = Interpolate(referenceFrames, MotionEasing.EaseRail(0.5d));
        scene.AppliedFrames.Last().Frame.Should().Be(expectedMiddleFrame);

        scene.FrameScheduler.RunNextFrame(
            TimeSpan.FromMilliseconds(delayMilliseconds + durationMilliseconds));
        await animationTask;

        scene.AppliedFrames.Last().Frame.Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }
}
