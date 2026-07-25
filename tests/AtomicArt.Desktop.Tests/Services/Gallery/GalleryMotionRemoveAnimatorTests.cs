using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionRemoveAnimatorTests : GalleryMotionAnimatorTestBase
{
    private const int RemovalDurationMilliseconds = 520;

    [Fact]
    public void AnimateRemovedItemAsync_WhenItemRemoved_UsesReferenceFramesAndSign()
    {
        GalleryMotionTestScene scene = GalleryMotionTestScene.Create();
        GalleryOperationCoordinator context = scene.Context;
        context.OverlayCanvas.Arrange(new Rect(0d, 0d, 800d, 600d));
        Guid itemId = Guid.NewGuid();
        GalleryAnimationTracker deleteOverlays = [];

        Task animationTask = scene.Animator.AnimateRemovedItemAsync(
            context,
            itemId,
            new Rect(600d, 20d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            deleteOverlays);

        animationTask.IsCompleted.Should().BeFalse();
        deleteOverlays.Should().ContainSingle();
        context.OverlayCanvas.Children.Should().ContainSingle();
        scene.AppliedFrames.Should().ContainSingle();
        scene.AppliedFrames[0].Frame.Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));

        scene.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scene.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(520d));

        scene.AppliedFrames.Last().Frame.Should().Be(new MotionFrame(38d, -30d, 0.72d, 8.5d, 0d));
    }

    [Fact]
    public void AnimateRemovedItemAsync_WithRemovalParticipant_SynchronizesInnerAnimation()
    {
        RecordingGalleryRemovalAnimationParticipantControl participant = new();
        GalleryMotionTestScene scene = GalleryMotionTestScene.Create(_ => participant);
        GalleryOperationCoordinator context = scene.Context;
        GalleryAnimationTracker deleteOverlays = [];

        _ = scene.Animator.AnimateRemovedItemAsync(
            context,
            Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            new Rect(20d, 20d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            deleteOverlays);

        participant.RemovalDurationMilliseconds.Should().Be(RemovalDurationMilliseconds);
    }
}
