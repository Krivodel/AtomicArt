using System.Reflection;

using Avalonia.Headless;
using Avalonia.Input;
using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class SmoothScrollBehaviorTests
{
    public static TheoryData<double, double> BoundaryTargets => new()
    {
        { SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength, 0d },
        { 0d, SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength }
    };
    public static TheoryData<double, Vector> BoundaryWheelSeries => new()
    {
        { 0d, new Vector(0d, -1d) },
        { SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength, new Vector(0d, 1d) }
    };

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<SmoothScrollTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public void TryCalculateTargetOffset_WithNegativeWheelDelta_ClampsToMaximum()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            SmoothScrollTestActions.AssertTargetOffsetY(
                host.Viewer,
                new Vector(0d, -20d),
                SmoothScrollTestConstants.ContentLength
                - SmoothScrollTestConstants.ViewportLength);
        });
    }

    [Fact]
    public void TryCalculateTargetOffset_WithPositiveWheelDelta_ClampsToZero()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            host.Viewer.Offset = new Vector(0d, 20d);

            SmoothScrollTestActions.AssertTargetOffsetY(
                host.Viewer,
                new Vector(0d, 20d),
                0d);
        });
    }

    [Fact]
    public void TryCalculateTargetOffset_WhenVerticalAvailable_UsesVerticalOffset()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            SmoothScrollTestActions.AssertTargetOffset(
                host.Viewer,
                new Vector(0d, -1d),
                new Vector(0d, SmoothScrollTestConstants.WheelMultiplier));
        });
    }

    [Fact]
    public void GetDuration_WithDefaultValue_ReturnsNoticeableSmoothingDuration()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            TimeSpan duration = SmoothScrollBehavior.GetDuration(host.Viewer);

            duration.Should().Be(TimeSpan.FromMilliseconds(240d));
        });
    }

    [Fact]
    public void GetWheelMultiplier_WithDefaultValue_ReturnsLargerWheelStep()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            double multiplier = SmoothScrollBehavior.GetWheelMultiplier(host.Viewer);

            multiplier.Should().Be(SmoothScrollTestConstants.WheelMultiplier);
        });
    }

    [Fact]
    public void ScrollToOffset_WithZeroDuration_AppliesTargetOffset()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateHorizontal();
            Vector targetOffset = new(SmoothScrollTestConstants.WheelMultiplier, 0d);
            SmoothScrollBehavior.SetDuration(host.Viewer, TimeSpan.Zero);

            SmoothScrollBehavior.ScrollToOffset(host.Viewer, targetOffset);

            host.Viewer.Offset.Should().Be(targetOffset);
        });
    }

    [Fact]
    public void TryCalculateTargetOffset_WhenOnlyHorizontalAvailable_UsesHorizontalOffset()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateHorizontal();

            SmoothScrollTestActions.AssertTargetOffset(
                host.Viewer,
                new Vector(0d, -1d),
                new Vector(SmoothScrollTestConstants.WheelMultiplier, 0d));
        });
    }

    [Fact]
    public void TryCalculateTargetOffset_WhenVerticalCannotMove_UsesHorizontalOffset()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateBothAxes();
            host.Viewer.Offset = new Vector(
                0d,
                SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength);

            SmoothScrollTestActions.AssertTargetOffset(
                host.Viewer,
                new Vector(0d, -1d),
                new Vector(
                    SmoothScrollTestConstants.WheelMultiplier,
                    SmoothScrollTestConstants.ContentLength
                    - SmoothScrollTestConstants.ViewportLength));
        });
    }

    [Fact]
    public void OnPointerWheelChanged_WhenViewerCannotMove_DoesNotHandleEvent()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateStatic();
            bool reachedParent = false;
            host.Parent.AddHandler(
                InputElement.PointerWheelChangedEvent,
                (_, _) => reachedParent = true);
            SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);

            SmoothScrollTestInput.Scroll(host.Window);

            reachedParent.Should().BeTrue();
        });
    }

    [Fact]
    public void SetIsEnabled_WhenEnabledTwice_DoesNotDuplicateSubscription()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollTestActions.EnableImmediate(host.Viewer);

            SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);
            SmoothScrollTestInput.Scroll(host.Window);

            host.Viewer.Offset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier);
        });
    }

    [Fact]
    public void OnPointerWheelChanged_WhenSmoothScrollStarts_DoesNotApplyAvaloniaDefaultStep()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollBehavior.SetDuration(host.Viewer, SmoothScrollTestConstants.ActiveDuration);
            SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);
            double offsetBeforeWheel = host.Viewer.Offset.Y;

            SmoothScrollTestInput.Scroll(host.Window);

            host.Viewer.Offset.Y.Should().Be(offsetBeforeWheel);
        });
    }

    [Fact]
    public async Task SetIsEnabled_WhenDisabled_StopsActiveTransition()
    {
        await SmoothScrollTestDispatcher.DispatchAsync(async () =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            await SmoothScrollTestActions.AssertActiveScrollStopsAsync(
                host,
                currentHost => SmoothScrollBehavior.SetIsEnabled(currentHost.Viewer, false));
        });
    }

    [Fact]
    public void SetIsEnabled_WhenDisabled_UsesAvaloniaDefaultScrolling()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);
            SmoothScrollBehavior.SetIsEnabled(host.Viewer, false);
            double offsetBeforeWheel = host.Viewer.Offset.Y;

            SmoothScrollTestInput.Scroll(host.Window);

            host.Viewer.Offset.Y.Should().BeGreaterThan(offsetBeforeWheel);
        });
    }

    [Fact]
    public async Task OnDetachedFromVisualTree_WhenAnimationIsActive_StopsAnimation()
    {
        await SmoothScrollTestDispatcher.DispatchAsync(async () =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();

            await SmoothScrollTestActions.AssertActiveScrollStopsAsync(
                host,
                SmoothScrollTestActions.Detach);
        });
    }

    [Fact]
    public void OnDetachedFromVisualTree_WhenStateExists_ClearsState()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollTestActions.StartActiveSmoothScroll(host);

            SmoothScrollTestActions.Detach(host);

            SmoothScrollBehavior.HasState(host.Viewer).Should().BeFalse();
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenReattachedAfterDetach_HandlesWheelOnce()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollTestActions.EnableImmediate(host.Viewer);
            SmoothScrollTestActions.Detach(host);
            host.Window.Content = host.Parent;
            host.Window.CaptureRenderedFrame();

            SmoothScrollTestInput.Scroll(host.Window);

            host.Viewer.Offset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier);
        });
    }

    [Fact]
    public void OnPointerWheelChanged_WhenAnimationIsActive_AccumulatesTargetFromPreviousTarget()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollState state = SmoothScrollTestActions.CreateActiveState(host.Viewer);

            bool calculated = SmoothScrollTargetCalculator.TryCalculateTargetOffset(
                host.Viewer,
                state,
                new Vector(0d, -1d),
                SmoothScrollTestConstants.WheelMultiplier,
                out Vector targetOffset);
            state.Start(targetOffset, TimeSpan.Zero);
            state.Stop();

            calculated.Should().BeTrue();
            host.Viewer.Offset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier * 2d);
        });
    }

    [Fact]
    public void Start_WhenWheelRepeatedDuringAnimation_DoesNotJumpFromCurrentOffset()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            SmoothScrollState state = SmoothScrollTestActions.CreateActiveState(host.Viewer);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            double offsetBeforeRepeat = host.Viewer.Offset.Y;

            SmoothScrollTargetCalculator.TryCalculateTargetOffset(
                host.Viewer,
                state,
                new Vector(0d, -1d),
                SmoothScrollTestConstants.WheelMultiplier,
                out Vector targetOffset).Should().BeTrue();
            state.Start(targetOffset, SmoothScrollTestConstants.ActiveDuration);
            double offsetAfterRepeat = host.Viewer.Offset.Y;
            state.Stop();

            offsetAfterRepeat.Should().Be(offsetBeforeRepeat);
            targetOffset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier * 2d);
        });
    }

    [Theory]
    [MemberData(nameof(BoundaryTargets))]
    public void Start_WhenBoundaryTargetReceivesDelayedFrame_DoesNotSnapToBoundary(
        double startOffsetY,
        double targetOffsetY)
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            host.Viewer.Offset = new Vector(0d, startOffsetY);
            host.Window.Content = null;
            host.Window.CaptureRenderedFrame();
            SmoothScrollState state = new(host.Viewer);
            MethodInfo stepMethod = GetStepMethod();
            state.Start(new Vector(0d, targetOffsetY), SmoothScrollBehavior.GetDuration(host.Viewer));
            InvokeStep(stepMethod, state, TimeSpan.Zero);
            double offsetBeforeDelayedFrame = host.Viewer.Offset.Y;

            InvokeStep(stepMethod, state, TimeSpan.FromSeconds(1d));
            double offsetAfterDelayedFrame = host.Viewer.Offset.Y;
            state.Stop();

            Math.Abs(offsetAfterDelayedFrame - offsetBeforeDelayedFrame)
                .Should()
                .BeLessThan(SmoothScrollTestConstants.WheelMultiplier);
            offsetAfterDelayedFrame.Should().NotBe(targetOffsetY);
        });
    }

    [Theory]
    [MemberData(nameof(BoundaryWheelSeries))]
    public void TryCalculateTargetOffset_WhenWheelSeriesRepeatsAtAnimatedBoundary_KeepsVisibleOffsetStable(
        double startOffsetY,
        Vector wheelDelta)
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollViewerHost host = SmoothScrollTestHostFactory.CreateVertical();
            host.Viewer.Offset = new Vector(0d, startOffsetY);
            SmoothScrollState state = new(host.Viewer);

            for (int i = 0; i < 4; i++)
            {
                SmoothScrollTargetCalculator.TryCalculateTargetOffset(
                    host.Viewer,
                    state,
                    wheelDelta,
                    SmoothScrollTestConstants.WheelMultiplier,
                    out Vector targetOffset).Should().BeTrue();
                state.Start(targetOffset, SmoothScrollTestConstants.BoundarySeriesDuration);
            }

            double offsetBeforeRepeatedWheel = host.Viewer.Offset.Y;

            Math.Abs(offsetBeforeRepeatedWheel - startOffsetY)
                .Should()
                .BeLessThan(1d);
            SmoothScrollTargetCalculator.TryCalculateTargetOffset(
                host.Viewer,
                state,
                wheelDelta,
                SmoothScrollTestConstants.WheelMultiplier,
                out Vector repeatedTargetOffset).Should().BeTrue();
            state.Start(repeatedTargetOffset, SmoothScrollTestConstants.BoundarySeriesDuration);
            double offsetAfterRepeatedWheel = host.Viewer.Offset.Y;
            state.Stop();
            double expectedBoundaryOffset = wheelDelta.Y < 0d
                ? SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength
                : 0d;

            repeatedTargetOffset.Y.Should().Be(expectedBoundaryOffset);
            Math.Abs(offsetAfterRepeatedWheel - offsetBeforeRepeatedWheel)
                .Should()
                .BeLessThan(1d);
        });
    }

    private static MethodInfo GetStepMethod()
    {
        MethodInfo? stepMethod = typeof(SmoothScrollState).GetMethod(
            "Step",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (stepMethod is null)
        {
            throw new InvalidOperationException("SmoothScrollState.Step method was not found.");
        }

        return stepMethod;
    }

    private static void InvokeStep(MethodInfo stepMethod, SmoothScrollState state, TimeSpan frameTime)
    {
        object?[] arguments = [frameTime];

        stepMethod.Invoke(state, arguments);
    }
}
