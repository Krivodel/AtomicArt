using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class CenteredWidthResizeGripTests : DesktopControlTestBase
{
    private const double InitialWidth = 640d;
    private const double WindowWidth = 1040d;
    private const double WindowHeight = 400d;
    private const double Tolerance = 0.1d;

    [Theory]
    [InlineData(HorizontalAlignment.Right, 100d, 840d)]
    [InlineData(HorizontalAlignment.Right, -100d, InitialWidth)]
    [InlineData(HorizontalAlignment.Right, 1000d, 1000d)]
    [InlineData(HorizontalAlignment.Left, -100d, 840d)]
    [InlineData(HorizontalAlignment.Left, 100d, InitialWidth)]
    [InlineData(HorizontalAlignment.Left, -1000d, 1000d)]
    public async Task DragDelta_WithCenteredPanel_ResizesWithinBounds(
        HorizontalAlignment alignment,
        double horizontalChange,
        double expectedWidth)
    {
        await DispatchAsync(() =>
        {
            Grid container = CreateResizeSurface(out Border target, out CenteredWidthResizeGrip grip, alignment);

            Show(container, WindowWidth, WindowHeight, window =>
            {
                grip.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent });

                grip.RaiseEvent(new VectorEventArgs
                {
                    RoutedEvent = Thumb.DragDeltaEvent,
                    Vector = new Vector(horizontalChange, 0d)
                });
                window.CaptureRenderedFrame();

                target.Bounds.Width.Should().BeApproximately(expectedWidth, Tolerance);
                target.Bounds.Center.X.Should().BeApproximately(container.Bounds.Width / 2d, Tolerance);
            });
        });
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public async Task Target_WhenWindowShrinksAndExpands_RestoresRequestedWidth(HorizontalAlignment alignment)
    {
        await DispatchAsync(() =>
        {
            Grid container = CreateResizeSurface(out Border target, out CenteredWidthResizeGrip grip, alignment);
            double direction = alignment == HorizontalAlignment.Left ? -1d : 1d;

            Show(container, WindowWidth, WindowHeight, window =>
            {
                grip.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent });
                grip.RaiseEvent(new VectorEventArgs
                {
                    RoutedEvent = Thumb.DragDeltaEvent,
                    Vector = new Vector(direction * 100d, 0d)
                });
                window.CaptureRenderedFrame();
                window.Width = 500d;
                window.CaptureRenderedFrame();
                target.Bounds.Width.Should().BeApproximately(460d, Tolerance);
                grip.RaiseEvent(new VectorEventArgs
                {
                    RoutedEvent = Thumb.DragDeltaEvent,
                    Vector = new Vector(direction * -100d, 0d)
                });

                window.Width = WindowWidth;
                window.CaptureRenderedFrame();

                target.Bounds.Width.Should().BeApproximately(840d, Tolerance);
            });
        });
    }

    [Theory]
    [InlineData(HorizontalAlignment.Right, Key.Right, 860d)]
    [InlineData(HorizontalAlignment.Right, Key.Left, 820d)]
    [InlineData(HorizontalAlignment.Left, Key.Left, 860d)]
    [InlineData(HorizontalAlignment.Left, Key.Right, 820d)]
    public async Task KeyDown_WithFocusedGrip_MovesSelectedEdgeInArrowDirection(
        HorizontalAlignment alignment,
        Key key,
        double expectedWidth)
    {
        await DispatchAsync(() =>
        {
            Grid container = CreateResizeSurface(out Border target, out CenteredWidthResizeGrip grip, alignment);
            target.MaxWidth = 840d;
            grip.KeyboardIncrement = 10d;

            Show(container, WindowWidth, WindowHeight, window =>
            {
                grip.Focus().Should().BeTrue();

                window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
                window.CaptureRenderedFrame();

                target.MaxWidth.Should().BeApproximately(expectedWidth, Tolerance);
                target.Bounds.Center.X.Should().BeApproximately(container.Bounds.Width / 2d, Tolerance);
            });
        });
    }

    private static Grid CreateResizeSurface(
        out Border target,
        out CenteredWidthResizeGrip grip,
        HorizontalAlignment alignment)
    {
        Grid container = new();
        target = new Border
        {
            MaxWidth = InitialWidth,
            Margin = new Thickness(20d)
        };
        grip = new CenteredWidthResizeGrip
        {
            HorizontalAlignment = alignment,
            Target = target,
            ResizeContainer = container,
            MinimumTargetWidth = InitialWidth
        };
        target.Child = grip;
        container.Children.Add(target);

        return container;
    }
}
