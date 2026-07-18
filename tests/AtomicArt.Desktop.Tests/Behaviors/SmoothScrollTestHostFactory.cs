using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Headless;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal static class SmoothScrollTestHostFactory
{
    internal static SmoothScrollViewerHost CreateVertical()
    {
        return Create(
            SmoothScrollTestConstants.ViewportLength,
            SmoothScrollTestConstants.ContentLength,
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Visible);
    }

    internal static SmoothScrollViewerHost CreateHorizontal()
    {
        return Create(
            SmoothScrollTestConstants.ContentLength,
            SmoothScrollTestConstants.ViewportLength,
            ScrollBarVisibility.Visible,
            ScrollBarVisibility.Disabled);
    }

    internal static SmoothScrollViewerHost CreateBothAxes()
    {
        return Create(
            SmoothScrollTestConstants.ContentLength,
            SmoothScrollTestConstants.ContentLength,
            ScrollBarVisibility.Visible,
            ScrollBarVisibility.Visible);
    }

    internal static SmoothScrollViewerHost CreateStatic()
    {
        return Create(
            SmoothScrollTestConstants.ViewportLength,
            SmoothScrollTestConstants.ViewportLength,
            ScrollBarVisibility.Visible,
            ScrollBarVisibility.Visible);
    }

    internal static SmoothScrollNestedViewerHost CreateNestedVertical()
    {
        ScrollViewer innerViewer = CreateViewer(
            SmoothScrollTestConstants.ViewportLength,
            SmoothScrollTestConstants.ContentLength,
            ScrollBarVisibility.Disabled,
            ScrollBarVisibility.Visible);
        ScrollViewer outerViewer = CreateNestedOuterViewer(innerViewer);
        Border parent = CreateParent(outerViewer);
        Window window = CreateWindow(parent);

        return new SmoothScrollNestedViewerHost
        {
            Window = window,
            OuterViewer = outerViewer,
            InnerViewer = innerViewer
        };
    }

    private static SmoothScrollViewerHost Create(
        double contentWidth,
        double contentHeight,
        ScrollBarVisibility horizontalVisibility,
        ScrollBarVisibility verticalVisibility)
    {
        ScrollViewer viewer = CreateViewer(
            contentWidth,
            contentHeight,
            horizontalVisibility,
            verticalVisibility);
        Border parent = CreateParent(viewer);
        Window window = CreateWindow(parent);

        return new SmoothScrollViewerHost
        {
            Window = window,
            Parent = parent,
            Viewer = viewer
        };
    }

    private static ScrollViewer CreateViewer(
        double contentWidth,
        double contentHeight,
        ScrollBarVisibility horizontalVisibility,
        ScrollBarVisibility verticalVisibility)
    {
        return new ScrollViewer
        {
            Width = SmoothScrollTestConstants.ViewportLength,
            Height = SmoothScrollTestConstants.ViewportLength,
            HorizontalScrollBarVisibility = horizontalVisibility,
            VerticalScrollBarVisibility = verticalVisibility,
            Content = new Border
            {
                Width = contentWidth,
                Height = contentHeight
            }
        };
    }

    private static ScrollViewer CreateNestedOuterViewer(ScrollViewer innerViewer)
    {
        return new ScrollViewer
        {
            Width = SmoothScrollTestConstants.ViewportLength,
            Height = SmoothScrollTestConstants.ViewportLength,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Border
            {
                Width = SmoothScrollTestConstants.ViewportLength,
                Height = SmoothScrollTestConstants.ContentLength,
                Child = innerViewer
            }
        };
    }

    private static Border CreateParent(ScrollViewer viewer)
    {
        return new Border
        {
            Child = viewer
        };
    }

    private static Window CreateWindow(Border parent)
    {
        Window window = new()
        {
            Width = SmoothScrollTestConstants.ViewportLength,
            Height = SmoothScrollTestConstants.ViewportLength,
            Content = parent
        };

        window.Show();
        window.CaptureRenderedFrame();

        return window;
    }
}
