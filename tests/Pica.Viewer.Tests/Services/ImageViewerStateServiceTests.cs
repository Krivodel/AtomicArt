using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageViewerStateServiceTests
{
    [Fact]
    public async Task SaveAsync_WithWindowPlacement_RoundTripsState()
    {
        using PicaTemporaryDirectory temporaryDirectory = PicaTemporaryDirectory.Create();
        string stateFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image-viewer.json");
        ImageViewerState state = new()
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
        ImageViewerStateService writer = CreateService(stateFilePath);

        await writer.SaveAsync(state, CancellationToken.None);
        ImageViewerStateService reader = CreateService(stateFilePath);

        ImageViewerState restoredState = await reader.LoadAsync(CancellationToken.None);

        restoredState.IsFilteringEnabled.Should().BeFalse();
        restoredState.MovementSpeed.Should().Be(2);
        restoredState.ZoomSpeed.Should().Be(1);
        restoredState.ExpandOnDoubleClick.Should().BeFalse();
        restoredState.IsFastLoadingEnabled.Should().BeTrue();
        restoredState.AllowFreeZoomOut.Should().BeTrue();
        restoredState.IsSmoothPanningEnabled.Should().BeTrue();
        restoredState.IsPanningInertiaEnabled.Should().BeTrue();
        restoredState.ResizeBehavior.Should().Be(WindowResizeBehavior.FitWhenWindowed);
        restoredState.RememberWindowPlacement.Should().BeTrue();
        restoredState.IsWindowed.Should().BeTrue();
        restoredState.WindowX.Should().Be(-1200);
        restoredState.WindowY.Should().Be(80);
        restoredState.WindowWidth.Should().Be(900d);
        restoredState.WindowHeight.Should().Be(506.25d);
    }

    [Fact]
    public async Task SaveAsync_WhenWindowPlacementIsNotRemembered_ClearsPlacement()
    {
        using PicaTemporaryDirectory temporaryDirectory = PicaTemporaryDirectory.Create();
        string stateFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image-viewer.json");
        ImageViewerState state = new()
        {
            RememberWindowPlacement = false,
            IsWindowed = true,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 900d,
            WindowHeight = 600d
        };
        ImageViewerStateService service = CreateService(stateFilePath);

        await service.SaveAsync(state, CancellationToken.None);

        ImageViewerState restoredState = await service.LoadAsync(CancellationToken.None);

        restoredState.RememberWindowPlacement.Should().BeFalse();
        restoredState.IsWindowed.Should().BeFalse();
        restoredState.WindowX.Should().BeNull();
        restoredState.WindowY.Should().BeNull();
        restoredState.WindowWidth.Should().BeNull();
        restoredState.WindowHeight.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithLegacyWindowPlacement_RestoresWindowedMode()
    {
        using PicaTemporaryDirectory temporaryDirectory = PicaTemporaryDirectory.Create();
        string stateFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image-viewer.json");
        const string legacyStateJson = """
            {
              "rememberWindowPlacement": true,
              "windowX": 120,
              "windowY": 80,
              "windowWidth": 900,
              "windowHeight": 600
            }
            """;
        await File.WriteAllTextAsync(stateFilePath, legacyStateJson, CancellationToken.None);
        ImageViewerStateService service = CreateService(stateFilePath);

        ImageViewerState restoredState = await service.LoadAsync(CancellationToken.None);

        restoredState.IsWindowed.Should().BeTrue();
        restoredState.IsFilteringEnabled.Should().BeTrue();
        restoredState.IsFastLoadingEnabled.Should().BeFalse();
        restoredState.AllowFreeZoomOut.Should().BeFalse();
        restoredState.IsSmoothPanningEnabled.Should().BeTrue();
        restoredState.IsPanningInertiaEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithInertiaWithoutSmoothPanning_DisablesInertia()
    {
        using PicaTemporaryDirectory temporaryDirectory = PicaTemporaryDirectory.Create();
        string stateFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "image-viewer.json");
        ImageViewerState state = new()
        {
            IsSmoothPanningEnabled = false,
            IsPanningInertiaEnabled = true
        };
        ImageViewerStateService service = CreateService(stateFilePath);

        await service.SaveAsync(state, CancellationToken.None);

        ImageViewerState restoredState = await service.LoadAsync(CancellationToken.None);

        restoredState.IsSmoothPanningEnabled.Should().BeFalse();
        restoredState.IsPanningInertiaEnabled.Should().BeFalse();
    }

    private static ImageViewerStateService CreateService(string stateFilePath)
    {
        return new ImageViewerStateService(
            stateFilePath,
            NullLogger<ImageViewerStateService>.Instance);
    }
}
