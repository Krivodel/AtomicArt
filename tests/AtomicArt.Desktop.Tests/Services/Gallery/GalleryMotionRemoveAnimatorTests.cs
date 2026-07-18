using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionRemoveAnimatorTests : GalleryMotionAnimatorTestBase
{
    [Fact]
    public void AnimateRemovedItemAsync_WhenItemRemoved_UsesReferenceFramesAndSign()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler = CreateAnimationScheduler(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        context.OverlayCanvas.Arrange(new Rect(0d, 0d, 800d, 600d));
        Guid itemId = Guid.NewGuid();
        GalleryAnimationTracker deleteOverlays = [];

        Task animationTask = animator.AnimateRemovedItemAsync(
            context,
            itemId,
            new Rect(600d, 20d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            deleteOverlays);

        animationTask.IsCompleted.Should().BeFalse();
        deleteOverlays.Should().ContainSingle();
        context.OverlayCanvas.Children.Should().ContainSingle();
        appliedFrames.Should().ContainSingle();
        appliedFrames[0].Frame.Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(520d));

        appliedFrames.Last().Frame.Should().Be(new MotionFrame(38d, -30d, 0.72d, 8.5d, 0d));
    }
}
