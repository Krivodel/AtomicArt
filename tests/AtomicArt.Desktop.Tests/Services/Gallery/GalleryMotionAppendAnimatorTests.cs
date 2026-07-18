using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionAppendAnimatorTests : GalleryMotionAnimatorTestBase
{
    [Fact]
    public async Task AnimateAppendBatchAsync_WhenBatchAppended_UsesReferenceFramesDurationDelayAndEase()
    {
        GalleryMotionTestScene scene = GalleryMotionTestScene.Create();
        GalleryOperationCoordinator context = scene.Context;
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Border firstControl = new();
        Border secondControl = new();
        context.CardControls[firstId] = firstControl;
        context.CardControls[secondId] = secondControl;

        Task animationTask = scene.Animator.AnimateAppendBatchAsync(
            context,
            new List<object> { firstId, secondId });

        scene.AppliedFrames.Should().HaveCount(2);
        scene.AppliedFrames[0].Control.Should().BeSameAs(firstControl);
        scene.AppliedFrames[0].Frame.Should().Be(new MotionFrame(0d, 14d, 0.96d, 0d, 0d));
        scene.AppliedFrames[1].Control.Should().BeSameAs(secondControl);
        scene.AppliedFrames[1].Frame.Should().Be(new MotionFrame(0d, 14d, 0.96d, 0d, 0d));

        scene.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scene.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(180d));

        MotionFrame middleFrame = scene.AppliedFrames.Last(frame => frame.Control == firstControl).Frame;
        middleFrame.Y.Should().BeApproximately(1.75d, 0.000000000001d);
        middleFrame.Scale.Should().BeApproximately(0.995d, 0.000000000001d);
        middleFrame.Opacity.Should().BeApproximately(0.875d, 0.000000000001d);

        scene.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(360d));
        animationTask.IsCompleted.Should().BeFalse();
        scene.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(388d));
        await animationTask;

        scene.AppliedFrames.Last(frame => frame.Control == secondControl)
            .Frame
            .Should()
            .Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }
}
