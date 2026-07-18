using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.GalleryAnimation;

public sealed class GalleryAnimationSchedulerTests
{
    [Fact]
    public void AnimateAsync_WithFrames_AppliesFirstFrameImmediately()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler = CreateScheduler(frameScheduler, appliedFrames);
        Border control = new();
        MotionFrame firstFrame = new(0d, 14d, 0.96d, 0d, 0d);
        MotionFrame lastFrame = new(0d, 0d, 1d, 0d, 1d);

        scheduler.AnimateAsync(
            control,
            [firstFrame, lastFrame],
            360,
            0,
            Identity);

        appliedFrames.Should().ContainSingle();
        appliedFrames[0].Control.Should().BeSameAs(control);
        appliedFrames[0].Frame.Should().Be(firstFrame);
        scheduler.HasActiveAnimations.Should().BeTrue();
        frameScheduler.RequestedFrameCount.Should().Be(1);
    }

    [Fact]
    public void AnimateAsync_BeforeDelayElapsed_DoesNotAdvancePastFirstFrame()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler = CreateScheduler(frameScheduler, appliedFrames);
        Border control = new();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);

        scheduler.AnimateAsync(
            control,
            [firstFrame, lastFrame],
            100,
            50,
            Identity);
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(40d));

        appliedFrames.Should().HaveCount(1);
        appliedFrames[0].Frame.Should().Be(firstFrame);
        scheduler.HasActiveAnimations.Should().BeTrue();
    }

    [Fact]
    public void AnimateAsync_AfterDelayElapsed_InterpolatesWithReferenceFormula()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler = CreateScheduler(frameScheduler, appliedFrames);
        Border control = new();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);

        scheduler.AnimateAsync(
            control,
            [firstFrame, lastFrame],
            100,
            50,
            Identity);
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));

        MotionFrame interpolatedFrame = appliedFrames[^1].Frame;
        interpolatedFrame.X.Should().Be(50d);
        interpolatedFrame.Y.Should().Be(25d);
        interpolatedFrame.Scale.Should().Be(1.5d);
        interpolatedFrame.Rotate.Should().Be(10d);
        interpolatedFrame.Opacity.Should().Be(0.5d);
        scheduler.HasActiveAnimations.Should().BeTrue();
    }

    [Fact]
    public async Task AnimateAsync_WhenRawProgressReachesOne_CompletesAndRemovesAnimation()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler = CreateScheduler(frameScheduler, appliedFrames);
        Border control = new();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);
        int completedCount = 0;

        Task animationTask = scheduler.AnimateAsync(
            control,
            [firstFrame, lastFrame],
            100,
            0,
            Identity,
            () =>
            {
                completedCount++;
            });
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));
        await animationTask;

        appliedFrames[^1].Frame.Should().Be(lastFrame);
        completedCount.Should().Be(1);
        animationTask.IsCompletedSuccessfully.Should().BeTrue();
        scheduler.HasActiveAnimations.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_WithActiveControl_CompletesTaskWithoutCompletedAction()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler = CreateScheduler(frameScheduler, appliedFrames);
        Border control = new();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);
        int completedCount = 0;

        Task animationTask = scheduler.AnimateAsync(
            control,
            [firstFrame, lastFrame],
            100,
            0,
            Identity,
            () =>
            {
                completedCount++;
            });
        scheduler.Cancel([control]);
        await animationTask;

        completedCount.Should().Be(0);
        scheduler.HasActiveAnimations.Should().BeFalse();
    }

    private static GalleryAnimationScheduler CreateScheduler(
        TestUiFrameScheduler frameScheduler,
        List<AppliedFrame> appliedFrames)
    {
        return new GalleryAnimationScheduler(
            frameScheduler,
            (control, frame) =>
            {
                appliedFrames.Add(new AppliedFrame(control, frame));
            });
    }

    private static double Identity(double value)
    {
        return value;
    }

    private sealed record AppliedFrame(Control Control, MotionFrame Frame);
}
