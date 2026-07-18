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
            TestUiFrameScheduler frameScheduler = new();
            ResizeScenario scenario = ShowResizeScenario(frameScheduler);

            try
            {
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);

                AssertThirdCardAtNarrowLayout(thirdCard);

                scenario.Window.Width = 980d;
                scenario.Window.CaptureRenderedFrame();

                thirdCard = GetThirdCard(scenario.Control, scenario.Items);
                AssertThirdCardMovedToWideLayout(thirdCard);
                AssertAllCardsRendered(scenario);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void ScrollViewerViewportChanged_WhenAttached_AnimatesExistingCardsToNewPositions()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ResizeScenario scenario = ShowResizeScenario(frameScheduler);

            try
            {
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);
                ScrollViewer scrollViewer = GetGalleryScrollViewer(scenario.Control);

                scrollViewer.SetCurrentValue(ScrollViewer.ViewportProperty, new Size(980d, 640d));
                scenario.Window.CaptureRenderedFrame();

                AssertThirdCardMovedToWideLayout(thirdCard);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void ScrollViewerBoundsChanged_WhenAttached_AnimatesExistingCardsToNewPositions()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ResizeScenario scenario = ShowResizeScenario(frameScheduler);

            try
            {
                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);
                ScrollViewer scrollViewer = GetGalleryScrollViewer(scenario.Control);

                scrollViewer.Arrange(new Rect(0d, 0d, 980d, 640d));

                AssertThirdCardMovedToWideLayout(thirdCard);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void ScrollViewerSizeChanged_WhenDetached_DoesNotRefreshOrAnimateClearedScene()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            DetachScenario scenario = ShowDetachScenario(frameScheduler);

            try
            {
                Canvas galleryPanel = GetGalleryPanel(scenario.Control);
                Canvas overlayCanvas = GetOverlayCanvas(scenario.Control);

                scenario.Window.Content = null;
                scenario.Window.CaptureRenderedFrame();
                int requestedFrameCount = frameScheduler.RequestedFrameCount;

                ChangeDetachedScene(scenario);
                AssertDetachedSceneIgnoredChanges(frameScheduler, requestedFrameCount, galleryPanel, overlayCanvas);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void BoundsChanged_WhenAnimationCompletes_LeavesCardsAtNewPositionsWithIdentityTransform()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ResizeScenario scenario = ShowResizeScenario(frameScheduler);

            try
            {
                scenario.Window.Width = 980d;
                scenario.Window.CaptureRenderedFrame();
                CompleteAnimation(frameScheduler);

                GenerationCardControl thirdCard = GetThirdCard(scenario.Control, scenario.Items);

                AssertThirdCardAtWideLayout(thirdCard);
                AssertIdentityTransform(thirdCard);
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }

    [Fact]
    public void BoundsChanged_DuringActiveResizeAnimation_CancelsOldTransformAndTargetsLatestLayout()
    {
        Dispatch(() =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ResizeScenario scenario = ShowResizeScenario(frameScheduler);

            try
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
            }
            finally
            {
                scenario.Window.Close();
            }
        });
    }
}
