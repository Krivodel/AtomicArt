using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class AnimatedGalleryControlResizeTests : AnimatedGalleryControlResizeTestBase
{
    [Fact]
    public void BoundsChanged_WhenAttached_AnimatesExistingCardsToNewPositions()
    {
        Dispatch(() =>
        {
            RunResizeScenario((_, scenario) =>
            {
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);

                AssertThirdCardAtNarrowLayout(thirdCard);

                scenario.Window.Width = 980d;
                scenario.Window.CaptureRenderedFrame();

                thirdCard = GetThirdCard(scenario.Control, scenario.Items);
                AssertThirdCardMovedToWideLayout(thirdCard);
                AssertAllCardsRendered(scenario);
            });
        });
    }

    [Fact]
    public void ScrollViewerViewportChanged_WhenAttached_AnimatesExistingCardsToNewPositions()
    {
        AssertScrollViewerResize((scrollViewer, scenario) =>
        {
            scrollViewer.SetCurrentValue(ScrollViewer.ViewportProperty, new Size(980d, 640d));
            scenario.Window.CaptureRenderedFrame();
        });
    }

    [Fact]
    public void ScrollViewerBoundsChanged_WhenAttached_AnimatesExistingCardsToNewPositions()
    {
        AssertScrollViewerResize((scrollViewer, _) =>
        {
            scrollViewer.Arrange(new Rect(0d, 0d, 980d, 640d));
        });
    }

    [Fact]
    public void ScrollViewerSizeChanged_WhenDetached_DoesNotRefreshOrAnimateClearedScene()
    {
        Dispatch(() =>
        {
            RunDetachScenario((frameScheduler, scenario) =>
            {
                Canvas galleryPanel = GetGalleryPanel(scenario.Control);
                Canvas overlayCanvas = GetOverlayCanvas(scenario.Control);

                scenario.Window.Content = null;
                scenario.Window.CaptureRenderedFrame();
                int requestedFrameCount = frameScheduler.RequestedFrameCount;

                ChangeDetachedScene(scenario);
                AssertDetachedSceneIgnoredChanges(frameScheduler, requestedFrameCount, galleryPanel, overlayCanvas);
            });
        });
    }

    [Fact]
    public void BoundsChanged_WhenAnimationCompletes_LeavesCardsAtNewPositionsWithIdentityTransform()
    {
        Dispatch(() =>
        {
            RunResizeScenario((frameScheduler, scenario) =>
            {
                scenario.Window.Width = 980d;
                scenario.Window.CaptureRenderedFrame();
                CompleteAnimation(frameScheduler);

                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);

                AssertThirdCardAtWideLayout(thirdCard);
                AssertIdentityTransform(thirdCard);
            });
        });
    }

    [Fact]
    public void BoundsChanged_DuringActiveResizeAnimation_CancelsOldTransformAndTargetsLatestLayout()
    {
        Dispatch(() =>
        {
            RunResizeScenario((frameScheduler, scenario) =>
            {
                BeginWideResizeAnimation(scenario);
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);

                GetTranslateTransform(thirdCard).X.Should().Be(-GalleryLayoutService.CardWidth * 2d);

                scenario.Window.Width = 560d;
                scenario.Window.CaptureRenderedFrame();

                AssertThirdCardRetargetedToNarrowLayout(thirdCard);

                CompleteAnimation(frameScheduler);

                AssertThirdCardAtNarrowLayout(thirdCard);
                AssertIdentityTransform(thirdCard);
            });
        });
    }

    private static void AssertScrollViewerResize(
        Action<ScrollViewer, ResizeScenario> resize)
    {
        Dispatch(() =>
        {
            RunResizeScenario((_, scenario) =>
            {
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);
                ScrollViewer scrollViewer = GetGalleryScrollViewer(scenario.Control);

                resize(scrollViewer, scenario);

                AssertThirdCardMovedToWideLayout(thirdCard);
            });
        });
    }
}
