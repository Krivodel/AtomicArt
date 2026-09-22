using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class Dlss5ComparisonPanelTests : DesktopControlTestBase
{
    [Theory]
    [InlineData(4096, 512, 1200, 600)]
    [InlineData(512, 4096, 1200, 600)]
    [InlineData(1920, 1080, 800, 240)]
    public void Layout_FitsBothImagesWithoutCropping(double imageWidth, double imageHeight, double width, double height)
    {
        Dispatch(() =>
        {
            Dlss5ComparisonPanel panel = new()
            {
                SourceWidth = imageWidth,
                SourceHeight = imageHeight,
                HasSource = true
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            Size available = new(width, height);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            Rect source = panel.Children[1].Bounds;
            Rect result = panel.Children[2].Bounds;
            source.Size.Should().Be(result.Size);
            double contentHeight = Dlss5ComparisonPanelTests.GetImageContentHeight(source);
            (source.Width / contentHeight).Should().BeApproximately(imageWidth / imageHeight, 0.0001);
            source.Left.Should().BeGreaterThanOrEqualTo(0);
            source.Top.Should().BeGreaterThanOrEqualTo(0);
            result.Right.Should().BeLessThanOrEqualTo(width);
            result.Bottom.Should().BeLessThanOrEqualTo(height);
        });
    }

    [Fact]
    public void ClearSource_ReturnsToOneFullWidthSurface()
    {
        Dispatch(() =>
        {
            Dlss5ComparisonPanel panel = new() { HasSource = true };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.HasSource = false;
            Size available = new(1000, 600);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            panel.Children[1].Opacity.Should().Be(0);
            panel.Children[2].Opacity.Should().Be(0);
            panel.Children[2].IsHitTestVisible.Should().BeFalse();
            panel.Children[3].Bounds.Size.Should().Be(available);
            panel.Children[3].Opacity.Should().Be(1);
            panel.Children[3].IsHitTestVisible.Should().BeTrue();
        });
    }

    [Fact]
    public void Transition_KeepsImageFramesAtSourceAspectRatio()
    {
        Dispatch(() =>
        {
            Dlss5ComparisonPanel panel = new()
            {
                SourceWidth = 1920,
                SourceHeight = 1080,
                HasSource = true,
                SplitProgress = 0.2
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            Size available = new(1200, 600);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            Rect source = panel.Children[1].Bounds;
            Rect result = panel.Children[2].Bounds;
            double placeholderOpacity = panel.Children[3].Opacity;
            source.Size.Should().Be(result.Size);
            double contentHeight = Dlss5ComparisonPanelTests.GetImageContentHeight(source);
            (source.Width / contentHeight).Should().BeApproximately(16d / 9d, 0.0001);
            source.Left.Should().BeLessThan(result.Left);
            panel.Children[3].Bounds.Size.Should().Be(available);
            panel.Children[3].Opacity.Should().BeInRange(0, 1);

            panel.SplitProgress = 0.75;
            panel.Children[1].Bounds.Should().Be(source);
            panel.Children[2].Bounds.Should().Be(result);
            panel.Children[3].Opacity.Should().Be(0);
        });
    }

    [Fact]
    public void Merge_AfterSourceIsCleared_KeepsPreviousAspectRatioDuringTransition()
    {
        Dispatch(() =>
        {
            Dlss5ComparisonPanel panel = new()
            {
                SourceWidth = 1920,
                SourceHeight = 1080,
                HasSource = true
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.HasSource = false;
            panel.SourceWidth = 0;
            panel.SourceHeight = 0;
            panel.SplitProgress = 0.5;
            Size available = new(1200, 600);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            Rect source = panel.Children[1].Bounds;
            double contentHeight = Dlss5ComparisonPanelTests.GetImageContentHeight(source);
            (source.Width / contentHeight).Should().BeApproximately(16d / 9d, 0.0001);
        });
    }

    [Fact]
    public void RenderingWithoutPanelTransition_ShowsProgressLineAtFullResultWidth()
    {
        Dispatch(() =>
        {
            Dlss5ComparisonPanel panel = new()
            {
                IsRendering = true,
                SourceWidth = 1920,
                SourceHeight = 1080,
                SplitProgress = 1d
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            Size available = new(1200, 600);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            Control progress = panel.Children[0];
            Rect result = panel.Children[2].Bounds;
            progress.IsVisible.Should().BeTrue();
            progress.Opacity.Should().Be(1);
            progress.Bounds.Width.Should().Be(result.Width);
            progress.Bounds.Height.Should().Be(Dlss5ComparisonPanel.ProgressHeight);
            progress.Bounds.Bottom.Should().Be(result.Bottom);
            progress.ZIndex.Should().BeGreaterThan(panel.Children[2].ZIndex);
            DoubleTransition fade = progress.Transitions.Should()
                .ContainSingle()
                .Which.Should()
                .BeOfType<DoubleTransition>()
                .Subject;
            fade.Property.Should().Be(Visual.OpacityProperty);
            fade.Duration.Should().Be(TimeSpan.FromMilliseconds(120));
        });
    }

    [Fact]
    public void OpeningAnimation_UsesCardDealStagesAndSmoothCrossFades()
    {
        Dlss5PanelAnimationState start = Dlss5ComparisonPanel.GetOpeningAnimationState(TimeSpan.Zero);
        Dlss5PanelAnimationState firstCardMidpoint = Dlss5ComparisonPanel.GetOpeningAnimationState(
            TimeSpan.FromMilliseconds(Dlss5ComparisonPanel.FirstCardDurationMilliseconds / 2d));
        Dlss5PanelAnimationState dealStart = Dlss5ComparisonPanel.GetOpeningAnimationState(
            TimeSpan.FromMilliseconds(
                Dlss5ComparisonPanel.FirstCardDurationMilliseconds
                + Dlss5ComparisonPanel.DealDelayMilliseconds));
        Dlss5PanelAnimationState dealInProgress = Dlss5ComparisonPanel.GetOpeningAnimationState(
            TimeSpan.FromMilliseconds(
                Dlss5ComparisonPanel.FirstCardDurationMilliseconds
                + Dlss5ComparisonPanel.DealDelayMilliseconds
                + 100d));
        Dlss5PanelAnimationState completed = Dlss5ComparisonPanel.GetOpeningAnimationState(
            TimeSpan.FromMilliseconds(
                Dlss5ComparisonPanel.FirstCardDurationMilliseconds
                + Dlss5ComparisonPanel.DealDelayMilliseconds
                + Dlss5ComparisonPanel.ResultCardDurationMilliseconds));

        start.Should().Be(new Dlss5PanelAnimationState(0d, 0d, 0d, 0d, 1d, 1d, 0d));
        firstCardMidpoint.FirstCardProgress.Should().BeInRange(0d, 1d);
        firstCardMidpoint.SourceOpacity.Should().BeApproximately(0.5d, 0.000001d);
        firstCardMidpoint.PlaceholderOpacity.Should().BeApproximately(0.5d, 0.000001d);
        dealStart.Should().Be(new Dlss5PanelAnimationState(1d, 0d, 1d, 0d, 0d, 1d, 0d));
        dealInProgress.ResultCardProgress.Should().BeInRange(0d, 1d);
        dealInProgress.ResultOpacity.Should().BeInRange(0d, 1d);
        dealInProgress.ResultScale.Should().BeGreaterThan(1d);
        dealInProgress.ResultRotation.Should().BeGreaterThan(0d);
        completed.Should().Be(new Dlss5PanelAnimationState(1d, 1d, 1d, 1d, 0d, 1d, 0d));
    }

    [Fact]
    public void ClosingAnimation_KeepsImagesWhileCardsReturnAndFadesThemSmoothly()
    {
        Dlss5PanelAnimationState start = Dlss5ComparisonPanel.GetClosingAnimationState(TimeSpan.Zero);
        Dlss5PanelAnimationState resultReturning = Dlss5ComparisonPanel.GetClosingAnimationState(
            TimeSpan.FromMilliseconds(Dlss5ComparisonPanel.ResultCardDurationMilliseconds * 0.8d));
        Dlss5PanelAnimationState firstCardMidpoint = Dlss5ComparisonPanel.GetClosingAnimationState(
            TimeSpan.FromMilliseconds(
                Dlss5ComparisonPanel.ResultCardDurationMilliseconds
                + Dlss5ComparisonPanel.DealDelayMilliseconds
                + (Dlss5ComparisonPanel.FirstCardDurationMilliseconds / 2d)));
        Dlss5PanelAnimationState completed = Dlss5ComparisonPanel.GetClosingAnimationState(
            TimeSpan.FromMilliseconds(
                Dlss5ComparisonPanel.ResultCardDurationMilliseconds
                + Dlss5ComparisonPanel.DealDelayMilliseconds
                + Dlss5ComparisonPanel.FirstCardDurationMilliseconds));

        start.Should().Be(new Dlss5PanelAnimationState(1d, 1d, 1d, 1d, 0d, 1d, 0d));
        resultReturning.ResultCardProgress.Should().BeInRange(0d, 1d);
        resultReturning.ResultOpacity.Should().BeInRange(0d, 1d);
        resultReturning.SourceOpacity.Should().Be(1d);
        firstCardMidpoint.FirstCardProgress.Should().BeInRange(0d, 1d);
        firstCardMidpoint.SourceOpacity.Should().BeApproximately(0.5d, 0.000001d);
        firstCardMidpoint.PlaceholderOpacity.Should().BeApproximately(0.5d, 0.000001d);
        completed.Should().Be(new Dlss5PanelAnimationState(0d, 0d, 0d, 0d, 1d, 1d, 0d));
    }

    [Fact]
    public void HasSource_WhenAttached_RunsBothAnimationStages()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            Dlss5ComparisonPanel panel = new(frameScheduler) { IsRendering = true };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            Window window = Show(panel, 1200, 600);

            try
            {
                panel.HasSource = true;
                panel.Children[0].IsVisible.Should().BeTrue();
                frameScheduler.RunNextFrame(TimeSpan.Zero);
                frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(
                    Dlss5ComparisonPanel.FirstCardDurationMilliseconds));

                panel.Children[1].Opacity.Should().Be(1d);
                panel.Children[2].Opacity.Should().Be(0d);
                panel.Children[3].Opacity.Should().Be(0);
                panel.Children[1].IsHitTestVisible.Should().BeFalse();
                panel.Children[0].IsVisible.Should().BeTrue();
                ((TransformGroup)panel.Children[2].RenderTransform!).Children
                    .OfType<TranslateTransform>()
                    .Single()
                    .X
                    .Should()
                    .BeNegative();

                frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(
                    Dlss5ComparisonPanel.FirstCardDurationMilliseconds
                    + Dlss5ComparisonPanel.DealDelayMilliseconds
                    + Dlss5ComparisonPanel.ResultCardDurationMilliseconds));

                panel.Children[1].IsHitTestVisible.Should().BeTrue();
                panel.Children[2].IsHitTestVisible.Should().BeTrue();
                panel.Children[0].IsVisible.Should().BeTrue();
                panel.Children[0].Should().BeOfType<Dlss5ProgressLine>()
                    .Which.IsActive.Should().BeTrue();
                panel.Children[1].RenderTransform.Should().BeOfType<TransformGroup>();
                panel.Children[1].RenderTransformOrigin.Should().Be(RelativePoint.Center);
                panel.Children[2].RenderTransformOrigin.Should().Be(RelativePoint.Center);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CloseSourceAsync_UntilCardsAreMerged_KeepsContentAndHidesProgress()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            Dlss5ComparisonPanel panel = new(frameScheduler)
            {
                HasSource = true,
                IsRendering = true
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            Window window = Show(panel, 1200, 600);

            try
            {
                Task merged = panel.CloseSourceAsync();
                frameScheduler.RunNextFrame(TimeSpan.Zero);
                frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(
                    Dlss5ComparisonPanel.ResultCardDurationMilliseconds * 0.8d));

                merged.IsCompleted.Should().BeFalse();
                panel.Children[1].Opacity.Should().BeGreaterThan(0d);
                panel.Children[2].Opacity.Should().BeGreaterThan(0d);
                panel.Children[0].IsVisible.Should().BeTrue();
                panel.Children[0].Should().BeOfType<Dlss5ProgressLine>()
                    .Which.IsActive.Should().BeFalse();

                frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(
                    Dlss5ComparisonPanel.ResultCardDurationMilliseconds
                    + Dlss5ComparisonPanel.DealDelayMilliseconds
                    + Dlss5ComparisonPanel.FirstCardDurationMilliseconds));
                await merged;
                panel.HasSource = false;
                panel.SourceWidth = 0d;
                panel.SourceHeight = 0d;
                frameScheduler.HasQueuedFrame.Should().BeFalse();
                panel.Measure(new Size(1200d, 600d));
                panel.Arrange(new Rect(0d, 0d, 1200d, 600d));

                panel.Children[1].Opacity.Should().Be(0d);
                panel.Children[2].Opacity.Should().Be(0d);
                panel.Children[0].IsVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static double GetImageContentHeight(Rect frame)
    {
        return frame.Height
            - Dlss5ComparisonPanel.CaptionHeight
            - Dlss5ComparisonPanel.ProgressHeight;
    }
}
