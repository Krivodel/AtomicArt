using Avalonia;
using Avalonia.Controls;
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
        Border card = AddRenderedCard(context, itemId);
        Rect cardRect = new(
            600d,
            20d,
            GalleryLayoutService.CardWidth,
            GalleryLayoutService.CardHeight);
        Control? removedCard = scene.Animator.PrepareRemovedItem(
            context,
            itemId,
            cardRect,
            deleteOverlays);

        Task animationTask = scene.Animator.AnimateRemovedItemAsync(
            context,
            removedCard!,
            cardRect,
            deleteOverlays);

        removedCard.Should().BeSameAs(card);
        animationTask.IsCompleted.Should().BeFalse();
        deleteOverlays.Should().ContainSingle().Which.Should().BeSameAs(card);
        context.OverlayCanvas.Children.Should().ContainSingle().Which.Should().BeSameAs(card);
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
        Guid itemId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        GalleryAnimationTracker deleteOverlays = [];
        AddRenderedCard(context, itemId, participant);
        Rect cardRect = new(
            20d,
            20d,
            GalleryLayoutService.CardWidth,
            GalleryLayoutService.CardHeight);
        Control? removedCard = scene.Animator.PrepareRemovedItem(
            context,
            itemId,
            cardRect,
            deleteOverlays);

        _ = scene.Animator.AnimateRemovedItemAsync(
            context,
            removedCard!,
            cardRect,
            deleteOverlays);

        participant.WasPreparedForRemovalTransfer.Should().BeTrue();
        participant.RemovalDurationMilliseconds.Should().Be(RemovalDurationMilliseconds);
    }

    private static Border AddRenderedCard(
        GalleryOperationCoordinator context,
        Guid itemId)
    {
        Border card = new();
        AddRenderedCard(context, itemId, card);

        return card;
    }

    private static void AddRenderedCard(
        GalleryOperationCoordinator context,
        Guid itemId,
        Control card)
    {
        context.CardControls.Add(itemId, card);
        context.GalleryPanel.Children.Add(card);
    }
}
