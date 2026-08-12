using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class ContextMenuRevealHostTests
{
    private static readonly Rect MenuBounds = new(10d, 20d, 200d, 100d);

    [Fact]
    public void AnimationDurations_WhenDefined_MatchRevealTiming()
    {
        ContextMenuRevealHost.OpeningDurationMilliseconds.Should().Be(200);
        ContextMenuRevealHost.WidthRevealDurationMilliseconds.Should().Be(120);
        ContextMenuRevealHost.HeightRevealDurationMilliseconds.Should().Be(180);
        ContextMenuRevealHost.OpacityRevealDurationMilliseconds.Should().Be(60);
    }

    [Fact]
    public void ApplyOpeningProgress_AtStart_UsesInitialVisibleArea()
    {
        MenuFlyoutPresenter presenter = new();
        ContextMenuRevealHost host = new(presenter);

        host.ApplyOpeningProgress(0d);

        host.WidthRatio.Should().Be(ContextMenuRevealHost.InitialWidthRatio);
        host.HeightRatio.Should().Be(ContextMenuRevealHost.InitialHeightRatio);
        host.Opacity.Should().Be(0d);
    }

    [Fact]
    public void ApplyOpeningProgress_AfterWidthDuration_CompletesOnlyWidthAndOpacity()
    {
        MenuFlyoutPresenter presenter = new();
        ContextMenuRevealHost host = new(presenter);
        double progress = (double)ContextMenuRevealHost.WidthRevealDurationMilliseconds
            / ContextMenuRevealHost.OpeningDurationMilliseconds;

        host.ApplyOpeningProgress(progress);

        host.WidthRatio.Should().Be(1d);
        host.HeightRatio.Should().BeLessThan(1d);
        host.Opacity.Should().Be(1d);
    }

    [Fact]
    public void ApplyOpeningProgress_AfterHeightDuration_CompletesVisibleArea()
    {
        MenuFlyoutPresenter presenter = new();
        ContextMenuRevealHost host = new(presenter);
        double progress = (double)ContextMenuRevealHost.HeightRevealDurationMilliseconds
            / ContextMenuRevealHost.OpeningDurationMilliseconds;

        host.ApplyOpeningProgress(progress);

        host.WidthRatio.Should().Be(1d);
        host.HeightRatio.Should().Be(1d);
        host.Opacity.Should().Be(1d);
    }

    [Fact]
    public void SetBoxShadows_WithPopupShadow_UsesExactShadowBoundsAsPadding()
    {
        Thickness borderThickness = new(1d);
        MenuFlyoutPresenter presenter = new()
        {
            BorderThickness = borderThickness
        };
        ContextMenuRevealHost host = new(presenter);
        BoxShadows boxShadows = BoxShadows.Parse("0 6 16 0 #80000000");
        Rect contentBounds = new(0d, 0d, 3d, 3d);
        Rect shadowSurfaceBounds = ContextMenuRevealHost.CalculateShadowBounds(
            contentBounds,
            borderThickness);
        Rect shadowBounds = boxShadows.TransformBounds(shadowSurfaceBounds);
        Thickness expectedPadding = new(
            Math.Max(0d, contentBounds.Left - shadowBounds.Left),
            Math.Max(0d, contentBounds.Top - shadowBounds.Top),
            Math.Max(0d, shadowBounds.Right - contentBounds.Right),
            Math.Max(0d, shadowBounds.Bottom - contentBounds.Bottom));

        host.SetBoxShadows(boxShadows);

        host.BoxShadows.Should().Be(boxShadows);
        host.Padding.Should().Be(expectedPadding);
    }

    [Fact]
    public void CalculateShadowBounds_WithMenuBorder_InsetsShadowUnderBorder()
    {
        Thickness borderThickness = new(1d);

        Rect result = ContextMenuRevealHost.CalculateShadowBounds(
            MenuBounds,
            borderThickness);

        result.Should().Be(new Rect(11d, 21d, 198d, 98d));
    }

    [Theory]
    [InlineData((int)ContextMenuRevealOrigin.TopLeft, 10d, 20d)]
    [InlineData((int)ContextMenuRevealOrigin.TopRight, 110d, 20d)]
    [InlineData((int)ContextMenuRevealOrigin.BottomLeft, 10d, 90d)]
    [InlineData((int)ContextMenuRevealOrigin.BottomRight, 110d, 90d)]
    public void CalculateRevealBounds_WithInitialArea_AnchorsToExpectedCorner(
        int originValue,
        double expectedX,
        double expectedY)
    {
        ContextMenuRevealOrigin origin = (ContextMenuRevealOrigin)originValue;

        Rect bounds = ContextMenuRevealHost.CalculateRevealBounds(
            MenuBounds,
            ContextMenuRevealHost.InitialWidthRatio,
            ContextMenuRevealHost.InitialHeightRatio,
            origin);

        bounds.Should().Be(new Rect(expectedX, expectedY, 100d, 30d));
    }
}
