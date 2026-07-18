using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class ImageDropBehaviorTests : AnimatedGalleryControlTestBase
{
    private const int OverlayHideWaitMilliseconds = 100;

    [Fact]
    public void AcceptsData_WithExternalFile_AcceptsOnlyExternalTarget()
    {
        Mock<IStorageFile> fileMock = new();
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));

        bool acceptsWindow = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.ExternalFiles);
        bool acceptsPanel = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.GalleryImage);

        acceptsWindow.Should().BeTrue();
        acceptsPanel.Should().BeFalse();
    }

    [Fact]
    public void AcceptsData_WithGalleryImage_AcceptsOnlyGenerationPanel()
    {
        Mock<IStorageFile> fileMock = new();
        DataTransfer dataTransfer = GalleryImageDragData.Create(fileMock.Object);

        bool acceptsWindow = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.ExternalFiles);
        bool acceptsPanel = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.GalleryImage);

        acceptsWindow.Should().BeFalse();
        acceptsPanel.Should().BeTrue();
    }

    [Theory]
    [InlineData(160d, 90d, true)]
    [InlineData(20d, 20d, false)]
    public void DragOver_WithHandledSibling_ActivatesOnlyInsideConfiguredDropArea(
        double x,
        double y,
        bool expectedIsActive)
    {
        Dispatch(() =>
        {
            Mock<IStorageFile> fileMock = new();
            DataTransfer dataTransfer = GalleryImageDragData.Create(fileMock.Object);
            Border child = new()
            {
                Background = Avalonia.Media.Brushes.Transparent
            };
            Border dropArea = new()
            {
                Width = 100d,
                Height = 100d,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            ImageDropOverlayControl overlay = new();
            Grid panel = new();
            panel.Children.Add(dropArea);
            panel.Children.Add(child);
            panel.Children.Add(overlay);
            ImageDropBehavior.SetDropArea(panel, dropArea);
            ImageDropBehavior.SetOverlay(panel, overlay);
            ImageDropBehavior.SetTargetKind(panel, ImageDropTargetKind.GalleryImage);
            ImageDropBehavior.SetIsEnabled(panel, true);
            child.AddHandler(
                DragDrop.DragOverEvent,
                MarkHandled,
                RoutingStrategies.Bubble);
            Window window = Show(panel, 320d, 180d);

            try
            {
                window.DragDrop(
                    new Point(x, y),
                    RawDragEventType.DragOver,
                    dataTransfer,
                    DragDropEffects.Copy,
                    RawInputModifiers.None);

                overlay.IsActive.Should().Be(expectedIsActive);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task DragLeave_AfterGalleryDrag_HidesPanelTarget()
    {
        await DispatchAsync(async () =>
        {
            Mock<IStorageFile> fileMock = new();
            DataTransfer dataTransfer = GalleryImageDragData.Create(fileMock.Object);
            ImageDropOverlayControl overlay = new();
            Grid panel = new();
            panel.Children.Add(overlay);
            ImageDropBehavior.SetOverlay(panel, overlay);
            ImageDropBehavior.SetTargetKind(panel, ImageDropTargetKind.GalleryImage);
            ImageDropBehavior.SetIsEnabled(panel, true);
            Window window = Show(panel, 320d, 180d);

            try
            {
                window.DragDrop(
                    new Point(160d, 90d),
                    RawDragEventType.DragEnter,
                    dataTransfer,
                    DragDropEffects.Copy,
                    RawInputModifiers.None);
                window.DragDrop(
                    new Point(160d, 90d),
                    RawDragEventType.DragLeave,
                    dataTransfer,
                    DragDropEffects.Copy,
                    RawInputModifiers.None);

                await Task.Delay(OverlayHideWaitMilliseconds);

                overlay.IsActive.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task CancelScheduledOverlayHide_AfterPendingHide_KeepsPanelTargetActive()
    {
        await DispatchAsync(async () =>
        {
            ImageDropOverlayControl overlay = new();
            Grid panel = new();
            panel.Children.Add(overlay);
            ImageDropBehavior.SetOverlay(panel, overlay);
            Window window = Show(panel, 320d, 180d);

            try
            {
                overlay.IsActive = true;
                ImageDropBehavior.ScheduleOverlayHide(panel);
                ImageDropBehavior.CancelScheduledOverlayHide(panel);

                await Task.Delay(OverlayHideWaitMilliseconds);

                overlay.IsActive.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void MarkHandled(object? sender, DragEventArgs e)
    {
        _ = sender;

        e.Handled = true;
    }
}
