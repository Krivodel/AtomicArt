using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.UiAnimation;

public sealed class UiAnimationSchedulerTests
{
    private static readonly MotionFrame StandardFirstFrame = new(0d, 0d, 1d, 0d, 0d);
    private static readonly MotionFrame StandardLastFrame = new(100d, 50d, 2d, 20d, 1d);

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

        StartDelayedAnimation(scenario);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(40d));

        scenario.AppliedFrames.Should().HaveCount(1);
        scenario.AppliedFrames[0].Frame.Should().Be(StandardFirstFrame);
        scenario.Scheduler.HasActiveAnimations.Should().BeTrue();
    }

    [Fact]
    public void AnimateAsync_AfterDelayElapsed_InterpolatesWithReferenceFormula()
    {
        SchedulerScenario scenario = CreateScenario();

        StartDelayedAnimation(scenario);
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
        TrackedAnimationScenario context = new();

        context.Scenario.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        context.Scenario.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(100d));
        await context.AnimationTask;

        context.Scenario.AppliedFrames[^1].Frame.Should().Be(StandardLastFrame);
        context.CompletedCount.Should().Be(1);
        context.AnimationTask.IsCompletedSuccessfully.Should().BeTrue();
        context.Scenario.Scheduler.HasActiveAnimations.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_WithActiveControl_CompletesTaskWithoutCompletedAction()
    {
        TrackedAnimationScenario context = new();

        context.Scenario.Scheduler.Cancel(new Control[] { context.Scenario.Control });
        await context.AnimationTask;

        context.CompletedCount.Should().Be(0);
        context.Scenario.Scheduler.HasActiveAnimations.Should().BeFalse();
    }

    private static SchedulerScenario CreateScenario()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        UiAnimationScheduler scheduler =
            UiAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        Border control = new();

        return new SchedulerScenario(frameScheduler, appliedFrames, scheduler, control);
    }

    private static void StartDelayedAnimation(SchedulerScenario scenario)
    {
        scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { StandardFirstFrame, StandardLastFrame },
            100,
            50,
            Identity);
        scenario.FrameScheduler.RunNextFrame(TimeSpan.Zero);
    }

    private static Task StartAnimation(
        SchedulerScenario scenario,
        Action completed)
    {
        return scenario.Scheduler.AnimateAsync(
            scenario.Control,
            new MotionFrame[] { StandardFirstFrame, StandardLastFrame },
            100,
            0,
            Identity,
            completed);
    }

    private static double Identity(double value)
    {
        return value;
    }

    private sealed record SchedulerScenario(
        TestUiFrameScheduler FrameScheduler,
        List<AppliedMotionFrame> AppliedFrames,
        UiAnimationScheduler Scheduler,
        Border Control);

    private sealed class TrackedAnimationScenario
    {
        public SchedulerScenario Scenario { get; }
        public Task AnimationTask { get; }
        public int CompletedCount { get; private set; }

        public TrackedAnimationScenario()
        {
            Scenario = CreateScenario();
            AnimationTask = StartAnimation(Scenario, OnCompleted);
        }

        private void OnCompleted()
        {
            CompletedCount++;
        }
    }
}
