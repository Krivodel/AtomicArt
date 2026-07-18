using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia;
using FluentAssertions;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestActions
{
    internal static SmoothScrollState CreateActiveState(ScrollViewer viewer)
    {
        SmoothScrollState state = new(viewer);
        state.Start(
            new Vector(0d, SmoothScrollTestConstants.WheelMultiplier),
            SmoothScrollTestConstants.ActiveDuration);

        return state;
    }

    internal static Vector CalculateTargetOffset(ScrollViewer viewer, Vector wheelDelta)
    {
        SmoothScrollState state = new(viewer);

        SmoothScrollTargetCalculator.TryCalculateTargetOffset(
            viewer,
            state,
            wheelDelta,
            SmoothScrollTestConstants.WheelMultiplier,
            out Vector targetOffset).Should().BeTrue();

        return targetOffset;
    }

    internal static void AssertTargetOffset(
        ScrollViewer viewer,
        Vector wheelDelta,
        Vector expectedTargetOffset)
    {
        Vector targetOffset = CalculateTargetOffset(viewer, wheelDelta);

        targetOffset.Should().Be(expectedTargetOffset);
    }

    internal static void AssertTargetOffsetY(
        ScrollViewer viewer,
        Vector wheelDelta,
        double expectedTargetOffsetY)
    {
        Vector targetOffset = CalculateTargetOffset(viewer, wheelDelta);

        targetOffset.Y.Should().Be(expectedTargetOffsetY);
    }

    internal static void EnableImmediate(ScrollViewer viewer)
    {
        SmoothScrollBehavior.SetDuration(viewer, TimeSpan.Zero);
        SmoothScrollBehavior.SetIsEnabled(viewer, true);
    }

    internal static void StartActiveSmoothScroll(SmoothScrollViewerHost host)
    {
        SmoothScrollBehavior.SetDuration(host.Viewer, SmoothScrollTestConstants.ActiveDuration);
        SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);
        SmoothScrollTestInput.Scroll(host.Window);
    }

    internal static void Detach(SmoothScrollViewerHost host)
    {
        host.Window.Content = null;
        host.Window.CaptureRenderedFrame();
    }

    internal static async Task AssertActiveScrollStopsAsync(
        SmoothScrollViewerHost host,
        Action<SmoothScrollViewerHost> stop)
    {
        StartActiveSmoothScroll(host);

        stop(host);
        double stoppedOffset = host.Viewer.Offset.Y;

        await FinishAnimationAsync();

        host.Viewer.Offset.Y.Should().Be(stoppedOffset);
    }

    internal static async Task FinishAnimationAsync()
    {
        await Task.Delay(SmoothScrollTestConstants.ActiveDuration + SmoothScrollTestConstants.DelayMargin);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);
    }
}
