using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia;
using FluentAssertions;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestActions
{
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

    internal static void StartActiveSmoothScroll(SmoothScrollViewerHost host)
    {
        SmoothScrollBehavior.SetDuration(host.Viewer, SmoothScrollTestConstants.ActiveDuration);
        SmoothScrollBehavior.SetIsEnabled(host.Viewer, true);
        SmoothScrollTestInput.Scroll(host.Window);
    }

    internal static async Task FinishAnimationAsync()
    {
        await Task.Delay(SmoothScrollTestConstants.ActiveDuration + SmoothScrollTestConstants.DelayMargin);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);
    }
}
