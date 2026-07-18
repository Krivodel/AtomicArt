using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class SmoothScrollNestedViewerBehaviorTests
{
    [Fact]
    public void OnPointerWheelChanged_WhenSourceInsideScrollableChild_UsesChildViewer()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollNestedViewerHost host = SmoothScrollTestHostFactory.CreateNestedVertical();
            SmoothScrollBehavior.SetDuration(host.OuterViewer, TimeSpan.Zero);
            SmoothScrollBehavior.SetDuration(host.InnerViewer, TimeSpan.Zero);
            SmoothScrollBehavior.SetIsEnabled(host.OuterViewer, true);
            SmoothScrollBehavior.SetIsEnabled(host.InnerViewer, true);

            SmoothScrollTestInput.Scroll(host.Window);

            host.OuterViewer.Offset.Y.Should().Be(0d);
            host.InnerViewer.Offset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier);
        });
    }

    [Fact]
    public void OnPointerWheelChanged_WhenSourceInsideStandardScrollableChild_UsesChildViewer()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollNestedViewerHost host = SmoothScrollTestHostFactory.CreateNestedVertical();
            SmoothScrollBehavior.SetDuration(host.OuterViewer, TimeSpan.Zero);
            SmoothScrollBehavior.SetIsEnabled(host.OuterViewer, true);
            double innerOffsetBeforeWheel = host.InnerViewer.Offset.Y;

            SmoothScrollTestInput.Scroll(host.Window);

            host.OuterViewer.Offset.Y.Should().Be(0d);
            host.InnerViewer.Offset.Y.Should().BeGreaterThan(innerOffsetBeforeWheel);
        });
    }

    [Fact]
    public void OnPointerWheelChanged_WhenStandardChildCannotMove_UsesOuterViewer()
    {
        SmoothScrollTestDispatcher.Dispatch(() =>
        {
            using SmoothScrollNestedViewerHost host = SmoothScrollTestHostFactory.CreateNestedVertical();
            SmoothScrollBehavior.SetDuration(host.OuterViewer, TimeSpan.Zero);
            SmoothScrollBehavior.SetIsEnabled(host.OuterViewer, true);
            host.InnerViewer.Offset = new Vector(
                0d,
                SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength);

            SmoothScrollTestInput.Scroll(host.Window);

            host.OuterViewer.Offset.Y.Should().Be(SmoothScrollTestConstants.WheelMultiplier);
            host.InnerViewer.Offset.Y.Should().Be(SmoothScrollTestConstants.ContentLength - SmoothScrollTestConstants.ViewportLength);
        });
    }
}
