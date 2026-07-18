using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.GalleryAnimation;
using AtomicArt.Desktop.Tests.Services.Gallery;

namespace AtomicArt.Desktop.Tests.Services.GalleryAnimation;

public sealed class GalleryAnimationSchedulerTests
{
    [Fact]
    public void AnimateAsync_WithFrames_AppliesFirstFrameImmediately()
    {
        SchedulerScenario scenario = CreateScenario();
        MotionFrame firstFrame = new(0d, 14d, 0.96d, 0d, 0d);
        MotionFrame lastFrame = new(0d, 0d, 1d, 0d, 1d);

        scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { firstFrame, lastFrame },
            360,
            0,
            Identity);

        scenario.AppliedFrames.Should().ContainSingle();
        scenario.AppliedFrames[0].Control.Should().BeSameAs(scenario.Control);
        scenario.AppliedFrames[0].Frame.Should().Be(firstFrame);
        scenario.Scheduler.HasActiveAnimations.Should().BeTrue();
        scenario.FrameScheduler.RequestedFrameCount.Should().Be(1);
    }

    [Fact]
    public void AnimateAsync_BeforeDelayElapsed_DoesNotAdvancePastFirstFrame()
    {
        SchedulerScenario scenario = CreateScenario();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);

        scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { firstFrame, lastFrame },
            100,
            50,
            Identity);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(40d));

        scenario.AppliedFrames.Should().HaveCount(1);
        scenario.AppliedFrames[0].Frame.Should().Be(firstFrame);
        scenario.Scheduler.HasActiveAnimations.Should().BeTrue();
    }

    [Fact]
    public void AnimateAsync_AfterDelayElapsed_InterpolatesWithReferenceFormula()
    {
        SchedulerScenario scenario = CreateScenario();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);

        scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { firstFrame, lastFrame },
            100,
            50,
            Identity);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));

        MotionFrame interpolatedFrame = scenario.AppliedFrames[^1].Frame;
        interpolatedFrame.X.Should().Be(50d);
        interpolatedFrame.Y.Should().Be(25d);
        interpolatedFrame.Scale.Should().Be(1.5d);
        interpolatedFrame.Rotate.Should().Be(10d);
        interpolatedFrame.Opacity.Should().Be(0.5d);
        scenario.Scheduler.HasActiveAnimations.Should().BeTrue();
    }

    [Fact]
    public async Task AnimateAsync_WhenRawProgressReachesOne_CompletesAndRemovesAnimation()
    {
        SchedulerScenario scenario = CreateScenario();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);
        int completedCount = 0;

        Task animationTask = scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { firstFrame, lastFrame },
            100,
            0,
            Identity,
            () =>
            {
                completedCount++;
            });
        scenario.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));
        await animationTask;

        scenario.AppliedFrames[^1].Frame.Should().Be(lastFrame);
        completedCount.Should().Be(1);
        animationTask.IsCompletedSuccessfully.Should().BeTrue();
        scenario.Scheduler.HasActiveAnimations.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_WithActiveControl_CompletesTaskWithoutCompletedAction()
    {
        SchedulerScenario scenario = CreateScenario();
        MotionFrame firstFrame = new(0d, 0d, 1d, 0d, 0d);
        MotionFrame lastFrame = new(100d, 50d, 2d, 20d, 1d);
        int completedCount = 0;

        Task animationTask = scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { firstFrame, lastFrame },
            100,
            0,
            Identity,
            () =>
            {
                completedCount++;
            });
        scenario.Scheduler.Cancel(new Control[] { scenario.Control });
        await animationTask;

        completedCount.Should().Be(0);
        scenario.Scheduler.HasActiveAnimations.Should().BeFalse();
    }

    private static SchedulerScenario CreateScenario()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        GalleryAnimationScheduler scheduler =
            GalleryAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        Border control = new();

        return new SchedulerScenario(frameScheduler, appliedFrames, scheduler, control);
    }

    private static double Identity(double value)
    {
        return value;
    }

    private sealed record SchedulerScenario(
        TestUiFrameScheduler FrameScheduler,
        List<AppliedMotionFrame> AppliedFrames,
        GalleryAnimationScheduler Scheduler,
        Border Control);
}
