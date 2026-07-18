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
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler animationScheduler = CreateAnimationScheduler(frameScheduler, appliedFrames);
        GalleryMotionAnimator animator = CreateAnimator(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Border firstControl = new();
        Border secondControl = new();
        context.CardControls[firstId] = firstControl;
        context.CardControls[secondId] = secondControl;

        Task animationTask = animator.AnimateAppendBatchAsync(context, new List<object> { firstId, secondId });

        appliedFrames.Should().HaveCount(2);
        appliedFrames[0].Control.Should().BeSameAs(firstControl);
        appliedFrames[0].Frame.Should().Be(new MotionFrame(0d, 14d, 0.96d, 0d, 0d));
        appliedFrames[1].Control.Should().BeSameAs(secondControl);
        appliedFrames[1].Frame.Should().Be(new MotionFrame(0d, 14d, 0.96d, 0d, 0d));

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(180d));

        MotionFrame middleFrame = appliedFrames.Last(frame => frame.Control == firstControl).Frame;
        middleFrame.Y.Should().BeApproximately(1.75d, 0.000000000001d);
        middleFrame.Scale.Should().BeApproximately(0.995d, 0.000000000001d);
        middleFrame.Opacity.Should().BeApproximately(0.875d, 0.000000000001d);

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(360d));
        animationTask.IsCompleted.Should().BeFalse();
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(388d));
        await animationTask;

        appliedFrames.Last(frame => frame.Control == secondControl)
            .Frame
            .Should()
            .Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }
}
