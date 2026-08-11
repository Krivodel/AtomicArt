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
    private const double SelectionPanelTintOpacity = 0.22d;

    [Fact]
    public void IsActive_WhenChanged_UpdatesBlurPanelAndInputState()
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
                AnimatedBlurBackdropControl animatedBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<AnimatedBlurBackdropControl>()
                    .Single();
                BlurBackdropControl blurBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Single();
                Border selectionPanel = overlay.FindControl<Border>("SelectionPanel")
                    ?? throw new InvalidOperationException("Selection panel was not found.");
                TranslateTransform translation = selectionPanel.RenderTransform
                    .Should()
                    .BeOfType<TranslateTransform>()
                    .Subject;
                selectionPanel.Transitions = null;
                translation.Transitions = null;
                animatedBackdrop.Transitions = null;
                blurBackdrop.Transitions = null;

                overlay.IsActive = true;

                overlay.IsHitTestVisible.Should().BeTrue();
                animatedBackdrop.IsActive.Should().BeTrue();
                animatedBackdrop.Opacity.Should().Be(1d);
                blurBackdrop.Intensity.Should().Be(1d);
                selectionPanel.Opacity.Should().Be(1d);
                translation.Y.Should().Be(0d);

                overlay.IsActive = false;

                overlay.IsHitTestVisible.Should().BeFalse();
                animatedBackdrop.IsActive.Should().BeFalse();
                animatedBackdrop.Opacity.Should().Be(0d);
                blurBackdrop.Intensity.Should().Be(0d);
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
    public void Layout_WhenRendered_UsesSharedBlurAndEqualWidthActions()
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
                SolidColorBrush panelBackground = selectionPanel.Background
                    .Should()
                    .BeOfType<SolidColorBrush>()
                    .Subject;

                overlay.GetVisualDescendants()
                    .OfType<AnimatedBlurBackdropControl>()
                    .Should()
                    .ContainSingle();
                panelBackground.Color.Should().Be(Colors.Black);
                panelBackground.Opacity.Should().Be(SelectionPanelTintOpacity);
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
