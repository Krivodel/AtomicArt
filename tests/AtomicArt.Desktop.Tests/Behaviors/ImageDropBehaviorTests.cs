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

        AssertAcceptedTargets(dataTransfer, true, false);
    }

    [Fact]
    public void AcceptsData_WithGalleryImage_AcceptsOnlyGenerationPanel()
    {
        DataTransfer dataTransfer = CreateGalleryImageDataTransfer();

        AssertAcceptedTargets(dataTransfer, false, true);
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
            DataTransfer dataTransfer = CreateGalleryImageDataTransfer();
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
            ConfigureGalleryDropTarget(panel, overlay);
            child.AddHandler(
                DragDrop.DragOverEvent,
                MarkHandled,
                RoutingStrategies.Bubble);
            Window window = ShowTestWindow(panel);

            try
            {
                RaiseDragDrop(
                    window,
                    new Point(x, y),
                    RawDragEventType.DragOver,
                    dataTransfer);

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
            DataTransfer dataTransfer = CreateGalleryImageDataTransfer();
            OverlayPanelTestContext context = CreateOverlayPanelContext();
            ConfigureGalleryDropTarget(context.Panel, context.Overlay);
            Window window = ShowTestWindow(context.Panel);

            try
            {
                Point pointerPosition = new(160d, 90d);
                RaiseDragDrop(
                    window,
                    pointerPosition,
                    RawDragEventType.DragEnter,
                    dataTransfer);
                RaiseDragDrop(
                    window,
                    pointerPosition,
                    RawDragEventType.DragLeave,
                    dataTransfer);

                await Task.Delay(OverlayHideWaitMilliseconds);

                context.Overlay.IsActive.Should().BeFalse();
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
            OverlayPanelTestContext context = CreateOverlayPanelContext();
            Window window = ShowTestWindow(context.Panel);

            try
            {
                context.Overlay.IsActive = true;
                ImageDropBehavior.ScheduleOverlayHide(context.Panel);
                ImageDropBehavior.CancelScheduledOverlayHide(context.Panel);

                await Task.Delay(OverlayHideWaitMilliseconds);

                context.Overlay.IsActive.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static DataTransfer CreateGalleryImageDataTransfer()
    {
        Mock<IStorageFile> fileMock = new();

        return GalleryImageDragData.Create(fileMock.Object);
    }

    private static OverlayPanelTestContext CreateOverlayPanelContext()
    {
        ImageDropOverlayControl overlay = new();
        Grid panel = new();
        panel.Children.Add(overlay);

        return new OverlayPanelTestContext(overlay, panel);
    }

    private static void AssertAcceptedTargets(
        DataTransfer dataTransfer,
        bool expectedExternalFiles,
        bool expectedGalleryImage)
    {
        bool acceptsWindow = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.ExternalFiles);
        bool acceptsPanel = ImageDropBehavior.AcceptsData(
            dataTransfer,
            ImageDropTargetKind.GalleryImage);

        acceptsWindow.Should().Be(expectedExternalFiles);
        acceptsPanel.Should().Be(expectedGalleryImage);
    }

    private static void ConfigureGalleryDropTarget(
        Grid panel,
        ImageDropOverlayControl overlay)
    {
        ImageDropBehavior.SetOverlay(panel, overlay);
        ImageDropBehavior.SetTargetKind(panel, ImageDropTargetKind.GalleryImage);
        ImageDropBehavior.SetIsEnabled(panel, true);
    }

    private static Window ShowTestWindow(Grid panel)
    {
        return Show(panel, 320d, 180d);
    }

    private static void RaiseDragDrop(
        Window window,
        Point position,
        RawDragEventType eventType,
        DataTransfer dataTransfer)
    {
        window.DragDrop(
            position,
            eventType,
            dataTransfer,
            DragDropEffects.Copy,
            RawInputModifiers.None);
    }

    private static void MarkHandled(object? sender, DragEventArgs e)
    {
        _ = sender;

        e.Handled = true;
    }

    private sealed record OverlayPanelTestContext(
        ImageDropOverlayControl Overlay,
        Grid Panel);
}
