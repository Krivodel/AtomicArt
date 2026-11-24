using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DragHover_WithReference_ShowsFeedbackAndOpensOnlyAfterDelay(bool leave)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            int opened = 0;
            Button button = new()
            {
                Width = 48d,
                Height = 48d,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Classes = { "Icon" }
            };
            DragHoverBehavior.SetCommand(button, new RelayCommand(() => opened++));
            ImageDropOverlayControl overlay = new();
            Grid panel = new() { Background = Brushes.Transparent, Children = { button, overlay } };
            ImageDropBehavior.SetTargetKind(panel, ImageDropTargetKind.GalleryImage);
            ImageDropBehavior.SetOverlay(panel, overlay);
            ImageDropBehavior.SetIsEnabled(panel, true);
            Window window = DesktopControlTestBase.Show(panel, 120d, 120d);

            try
            {
                window.CaptureRenderedFrame()?.Dispose();
                Point point = button.TranslatePoint(new Point(24d, 24d), window) ?? throw new InvalidOperationException();
                IDataTransfer data = AtomicArtImageDragData.Create(Mock.Of<IStorageFile>(), AtomicArtImageDragSourceKind.Gallery);
                window.DragDrop(new Point(4d, 4d), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
                overlay.IsActive.Should().BeTrue();
                window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);

                overlay.IsActive.Should().BeFalse();
                button.Classes.Should().Contain(DragHoverState.ActiveClass);
                opened.Should().Be(0);
                await Task.Delay(DragHoverState.Delay / 2d);
                opened.Should().Be(0);
                Color hoverColor = Assert.IsType<Color>(button.FindResource("SukiPrimaryColor5"));
                Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color.Should().Be(hoverColor);

                if (leave)
                {
                    button.RaiseEvent(new DragEventArgs(DragDrop.DragLeaveEvent, data, button,
                        new Point(-1d, -1d), KeyModifiers.None));
                }

                await Task.Delay(DragHoverState.Delay);
                opened.Should().Be(leave ? 0 : 1);
                button.Classes.Contains(DragHoverState.ActiveClass).Should().Be(!leave);
                window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
                button.Classes.Should().NotContain(DragHoverState.ActiveClass);
                window.DragDrop(new Point(4d, 4d), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
                overlay.IsActive.Should().BeTrue();
            }
            finally
            {
                DragHoverBehavior.SetCommand(button, null);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithGalleryDropIntoEditor_AttachesOnceWithoutChangingGenerationPanel()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(
                new[] { new GenerationChatMessage(false, "Original", new DateTime(2026, 11, 24), Array.Empty<GenerationReference>()) },
                includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat };
            Grid panel = new() { Children = { view } };
            int parentAttachments = 0;
            ImageDropBehavior.SetTargetKind(panel, ImageDropTargetKind.GalleryImage);
            ImageDropBehavior.SetIsEnabled(panel, true);
            ImageAttachmentBehavior.SetAttachImagesCommand(panel,
                new AsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>(_ =>
                {
                    parentAttachments++;
                    return Task.CompletedTask;
                }));
            AttachedImageDto image = new("drop.png", "image/png", GalleryThumbnailTestImages.CreatePngBytes(32, 32));
            Mock<IDragDropImageService> drops = new();
            drops.Setup(service => service.ExtractImagesAsync(It.IsAny<IDataTransfer>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ImageAttachmentInput[] { ImageAttachmentInput.FromImage(image) });
            ImageDropBehavior.SetDragDropImageService(panel, drops.Object);
            Window window = DesktopControlTestBase.Show(panel, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                GenerationChatMessageView row = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Single();
                GenerationChatMessageViewModel message = context.Chat.Messages.Single();
                message.EditCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => row.EditorProgress == 1d);
                TextBox editor = row.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                Point point = editor.TranslatePoint(new Point(20d, 12d), window) ?? throw new InvalidOperationException();
                IDataTransfer data = AtomicArtImageDragData.Create(Mock.Of<IStorageFile>(), AtomicArtImageDragSourceKind.Gallery);

                window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
                window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.Images.Count == 1);

                parentAttachments.Should().Be(0);
                drops.Verify(service => service.ExtractImagesAsync(It.IsAny<IDataTransfer>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()), Times.Once);
                message.IsEditing.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
