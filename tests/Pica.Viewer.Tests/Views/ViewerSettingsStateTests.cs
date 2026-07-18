using Avalonia;
using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Views;

public sealed class ViewerSettingsStateTests
{
    [Fact]
    public void CreateImageViewerState_WithRememberedPlacement_PreservesSettingsAndPlacement()
    {
        ImageViewerState initialState = new()
        {
            IsFilteringEnabled = false,
            MovementSpeed = 2,
            ZoomSpeed = 1,
            ExpandOnDoubleClick = false,
            IsFastLoadingEnabled = true,
            AllowFreeZoomOut = true,
            IsSmoothPanningEnabled = true,
            IsPanningInertiaEnabled = true,
            ResizeBehavior = WindowResizeBehavior.FitWhenWindowed,
            RememberWindowPlacement = true,
            IsWindowed = true,
            WindowX = -1200,
            WindowY = 80,
            WindowWidth = 900d,
            WindowHeight = 506.25d
        };
        ViewerSettingsState settings = ViewerSettingsState.Create(initialState);

        ImageViewerState result = settings.CreateImageViewerState(
            true,
            new PixelPoint(-1200, 80),
            new Size(900d, 506.25d));

        result.Should().BeEquivalentTo(initialState);
    }

    [Fact]
    public void CreateImageViewerState_WithoutRememberedPlacement_ClearsPlacement()
    {
        ImageViewerState initialState = new()
        {
            RememberWindowPlacement = false
        };
        ViewerSettingsState settings = ViewerSettingsState.Create(initialState);

        ImageViewerState result = settings.CreateImageViewerState(
            true,
            new PixelPoint(100, 200),
            new Size(900d, 600d));

        result.RememberWindowPlacement.Should().BeFalse();
        result.IsWindowed.Should().BeFalse();
        result.WindowX.Should().BeNull();
        result.WindowY.Should().BeNull();
        result.WindowWidth.Should().BeNull();
        result.WindowHeight.Should().BeNull();
    }
}
