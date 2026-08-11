using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GallerySelectionOverlayViewTests : AnimatedGalleryControlTestBase
{
    private const double OverlayHeight = 320d;
    private const double OverlayWidth = 640d;

    private static readonly TimeSpan SelectionFadeDuration =
        TimeSpan.FromMilliseconds(180d);
    private static readonly TimeSpan SelectionSlideDuration =
        TimeSpan.FromMilliseconds(240d);

    [Fact]
    public void IsActive_WhenChanged_UpdatesPanelAndInputState()
    {
        Dispatch(() =>
        {
            using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
            GallerySelectionOverlayView overlay = new()
            {
                DataContext = viewModel
            };
            Window window = Show(overlay, OverlayWidth, OverlayHeight);

            try
            {
                Border selectionPanel = overlay.FindControl<Border>("SelectionPanel")
                    ?? throw new InvalidOperationException("Selection panel was not found.");
                TranslateTransform translation = selectionPanel.RenderTransform
                    .Should()
                    .BeOfType<TranslateTransform>()
                    .Subject;
                selectionPanel.Transitions = null;
                translation.Transitions = null;

                overlay.IsActive = true;

                overlay.IsHitTestVisible.Should().BeTrue();
                selectionPanel.Opacity.Should().Be(1d);
                translation.Y.Should().Be(0d);

                overlay.IsActive = false;

                overlay.IsHitTestVisible.Should().BeFalse();
                selectionPanel.Opacity.Should().Be(0d);
                translation.Y.Should().BeLessThanOrEqualTo(-OverlayHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Layout_WhenRendered_UsesTransparentAnimatedPanelAndEqualWidthActions()
    {
        Dispatch(() =>
        {
            using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
            GallerySelectionOverlayView overlay = new()
            {
                DataContext = viewModel,
                IsActive = true
            };
            Window window = Show(overlay, OverlayWidth, OverlayHeight);

            try
            {
                Grid actions = overlay
                    .GetVisualDescendants()
                    .OfType<Grid>()
                    .Single(grid => grid.ColumnDefinitions.Count == 3);
                Border selectionPanel = overlay.FindControl<Border>("SelectionPanel")
                    ?? throw new InvalidOperationException("Selection panel was not found.");
                IReadOnlyList<Button> buttons = actions.Children
                    .OfType<Button>()
                    .ToList();
                Transitions panelTransitions = selectionPanel.Transitions
                    ?? throw new InvalidOperationException(
                        "Selection panel transitions were not found.");
                DoubleTransition fadeTransition = panelTransitions
                    .OfType<DoubleTransition>()
                    .Single();
                TranslateTransform translation = selectionPanel.RenderTransform
                    .Should()
                    .BeOfType<TranslateTransform>()
                    .Subject;
                Transitions translationTransitions = translation.Transitions
                    ?? throw new InvalidOperationException(
                        "Selection panel translation transitions were not found.");
                DoubleTransition slideTransition = translationTransitions
                    .OfType<DoubleTransition>()
                    .Single();

                overlay.GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Should()
                    .BeEmpty();
                selectionPanel.Background.Should().BeNull();
                fadeTransition.Duration.Should().Be(SelectionFadeDuration);
                slideTransition.Duration.Should().Be(SelectionSlideDuration);
                buttons.Should().HaveCount(3);
                buttons.Select(button => button.Bounds.Width)
                    .Should()
                    .OnlyContain(width => width > 0d);
                buttons.Max(button => button.Bounds.Width)
                    .Should()
                    .BeApproximately(buttons.Min(button => button.Bounds.Width), 0.1d);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
