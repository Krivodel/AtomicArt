using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

[Collection(nameof(ScrollFadeRenderingCollection))]
public sealed class ScrollFadeMaskBehaviorTests : AnimatedGalleryControlTestBase
{
    private const int Center = 50;

    public new static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DesktopControlTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public async Task ScrollViewer_WhenOverflowDisappearsAndReturns_ReusesItsMaskOnlyWhenNeeded(Orientation orientation)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(ScrollFadeMaskBehaviorTests), () =>
        {
            bool horizontal = orientation == Orientation.Horizontal;
            Border content = new()
            {
                Width = horizontal ? 300d : 100d,
                Height = horizontal ? 100d : 300d,
                Background = Brushes.White
            };
            ScrollViewer scroll = new()
            {
                Content = content,
                HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden
            };
            Window window = DesktopControlTestBase.Show(scroll, 100d, 100d);

            try
            {
                ScrollContentPresenter presenter = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().Single();
                ScrollFadeMaskBehavior.SetOrientation(presenter, orientation);
                ScrollFadeMaskBehavior.SetFadeWhenClipped(presenter, true);
                ScrollFadeMaskBehavior.SetInsets(presenter, horizontal ? new Thickness(20d, 0d) : new Thickness(0d, 20d));
                LinearGradientBrush mask = presenter.OpacityMask.Should().BeOfType<LinearGradientBrush>().Subject;

                content.Width = 80d;
                content.Height = 80d;
                window.CaptureRenderedFrame()?.Dispose();

                presenter.OpacityMask.Should().BeNull();

                content.Width = horizontal ? 300d : 100d;
                content.Height = horizontal ? 100d : 300d;
                window.CaptureRenderedFrame()?.Dispose();

                presenter.OpacityMask.Should().BeSameAs(mask);
                mask.GradientStops[0].Color.Should().Be(Colors.White);
                mask.GradientStops[3].Color.Should().Be(Colors.Transparent);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public async Task ScrollViewer_WhenScrolled_RendersFadeAtBothViewportEdges(Orientation orientation)
    {
        await DispatchAsync(typeof(ScrollFadeMaskBehaviorTests), () =>
        {
            bool horizontal = orientation == Orientation.Horizontal;
            Border content = new()
            {
                Width = horizontal ? 300d : 100d,
                Height = horizontal ? 100d : 300d,
                Background = Brushes.White
            };
            ScrollViewer scrollViewer = new()
            {
                Width = 100d,
                Height = 100d,
                Content = content,
                HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden
            };

            Border host = new()
            {
                Width = 100d,
                Height = 100d,
                Background = Brushes.Black,
                Child = scrollViewer
            };
            Window window = Show(host, 100d, 100d);

            try
            {
                ScrollContentPresenter scrollPresenter = scrollViewer
                    .GetVisualDescendants()
                    .OfType<ScrollContentPresenter>()
                    .Single();
                ScrollFadeMaskBehavior.SetOrientation(scrollPresenter, orientation);
                ScrollFadeMaskBehavior.SetFadeWhenClipped(scrollPresenter, horizontal);
                ScrollFadeMaskBehavior.SetInsets(
                    scrollPresenter,
                    horizontal ? new Thickness(20d, 0d) : new Thickness(0d, 20d));
                scrollViewer.Offset = horizontal ? new Vector(100d, 0d) : new Vector(0d, 100d);
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                byte ReadBrightness(int position)
                {
                    return (horizontal ? bitmap.GetPixel(position, Center) : bitmap.GetPixel(Center, position)).Red;
                }

                byte center = ReadBrightness(Center);
                ReadBrightness(0).Should().BeLessThan(ReadBrightness(10));
                ReadBrightness(10).Should().BeLessThan(center);
                ReadBrightness(99).Should().BeLessThan(ReadBrightness(89));
                ReadBrightness(89).Should().BeLessThan(center);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
