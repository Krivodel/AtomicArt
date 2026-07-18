using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class AnimatedGalleryControlScrollTests : AnimatedGalleryControlTestBase
{
    private const int VirtualizationItemCount = 30;
    private const int VirtualizationScrollRows = 4;
    private const int FirstItemIndex = 0;
    private const int ExpectedScrolledItemIndex = 8;
    private const double OffsetTolerance = 0.001d;
    private const double TransparentBottomFadeOffset = 1d;
    private const double ExpectedGalleryWheelMultiplier = 192d;

    [Fact]
    public void GalleryScrollViewer_WhenCreated_UsesDoubleWheelStep()
    {
        Dispatch(() =>
        {
            AnimatedGalleryControl control = new(CreateSceneFactory());
            Window window = Show(control, 560d, 640d);

            try
            {
                ScrollViewer scrollViewer = GetGalleryScrollViewer(control);

                SmoothScrollBehavior.GetWheelMultiplier(scrollViewer)
                    .Should()
                    .Be(ExpectedGalleryWheelMultiplier);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ScrollViewerOffsetChanged_WhenAttached_RefreshesVirtualizedCards()
    {
        Dispatch(() =>
        {
            List<GenerationItemViewModel> items = CreateItems();
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = items
            };
            Window window = Show(control, 560d, 640d);

            try
            {
                ScrollViewer scrollViewer = GetGalleryScrollViewer(control);

                scrollViewer.Offset = new Vector(0d, GalleryLayoutService.CardHeight * VirtualizationScrollRows);
                window.CaptureRenderedFrame();

                GetVisibleItems(control).Should().Contain(items[ExpectedScrolledItemIndex]);
                GetVisibleItems(control).Should().NotContain(items[FirstItemIndex]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GalleryScrollViewer_WhenCreated_HasSmoothBottomOpacityMask()
    {
        Dispatch(() =>
        {
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = CreateItems()
            };
            Window window = Show(control, 560d, 640d);

            try
            {
                ScrollViewer scrollViewer = GetGalleryScrollViewer(control);

                LinearGradientBrush opacityMask = GetOpacityMask(scrollViewer);
                opacityMask.StartPoint.Should().Be(new RelativePoint(0d, 0d, RelativeUnit.Relative));
                opacityMask.EndPoint.Should().Be(new RelativePoint(0d, 1d, RelativeUnit.Relative));
                opacityMask.GradientStops.Should().HaveCount(3);
                opacityMask.GradientStops[1].Offset.Should().BeApproximately(
                    AnimatedGalleryControl.CalculateBottomFadeStartOffset(scrollViewer.Bounds.Height),
                    OffsetTolerance);
                opacityMask.GradientStops[1].Color.A.Should().Be(byte.MaxValue);
                opacityMask.GradientStops[2].Offset.Should().Be(TransparentBottomFadeOffset);
                opacityMask.GradientStops[2].Color.A.Should().Be(0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(360d)]
    [InlineData(640d)]
    public void GalleryScrollViewer_WithDifferentHeights_KeepsFixedBottomFadeHeight(double windowHeight)
    {
        Dispatch(() =>
        {
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = CreateItems()
            };
            Window window = Show(control, 560d, windowHeight);

            try
            {
                ScrollViewer scrollViewer = GetGalleryScrollViewer(control);
                LinearGradientBrush opacityMask = GetOpacityMask(scrollViewer);
                double actualFadeHeight = scrollViewer.Bounds.Height
                                          * (TransparentBottomFadeOffset - opacityMask.GradientStops[1].Offset);

                actualFadeHeight.Should().BeApproximately(
                    AnimatedGalleryControl.BottomOpacityFadeHeight,
                    OffsetTolerance);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static List<GenerationItemViewModel> CreateItems()
    {
        List<GenerationItemViewModel> items = [];

        for (int i = 0; i < VirtualizationItemCount; i++)
        {
            items.Add(CreateItem());
        }

        return items;
    }

    private static List<object?> GetVisibleItems(AnimatedGalleryControl control)
    {
        return GetGalleryPanel(control)
            .Children
            .OfType<GenerationCardControl>()
            .Select(card => card.DataContext)
            .ToList();
    }

    private static LinearGradientBrush GetOpacityMask(ScrollViewer scrollViewer)
    {
        return scrollViewer.OpacityMask
            .Should()
            .BeOfType<LinearGradientBrush>()
            .Subject;
    }
}
