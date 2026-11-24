using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
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
    public async Task ChatView_WithShortMessageEditor_PreservesTextOriginAndFitsActions(bool assistant)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatMessage saved = new(assistant, "Hi", new DateTime(2026, 11, 24), Array.Empty<GenerationReference>());
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(
                new GenerationChatHistory(new[] { saved }, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 380d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                window.CaptureRenderedFrame()?.Dispose();
                GenerationChatMessageView message = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Single();
                GenerationChatMessageViewModel model = context.Chat.Messages.Single();
                TextBox editor = message.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                TextPresenter presenter = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
                Point origin = presenter.TranslatePoint(default, message) ?? throw new InvalidOperationException();
                double width = editor.Bounds.Width;

                model.EditCommand.Execute(null);
                window.UpdateLayout();
                editor.Bounds.Width.Should().BeApproximately(width, 1d);
                (presenter.TranslatePoint(default, message) ?? throw new InvalidOperationException()).Should().Be(origin);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.EditorProgress == 1d);
                editor.CaretIndex.Should().Be(saved.Text.Length);
                editor.SelectionStart.Should().Be(editor.SelectionEnd);
                editor.Bounds.Width.Should().BeGreaterThan(presenter.TextLayout.Width + editor.Padding.Left + editor.Padding.Right);
                ScrollContentPresenter scroller = editor.GetVisualDescendants().OfType<ScrollContentPresenter>().Single();
                scroller.OpacityMask.Should().BeNull();
                GenerationChatMessagePanel layout = message.GetVisualDescendants().OfType<GenerationChatMessagePanel>().Single();
                Control bubble = layout.Children[0];
                Button cancel = layout.Children[2].GetVisualDescendants().OfType<Button>().First();
                Point button = cancel.TranslatePoint(default, layout) ?? throw new InvalidOperationException();
                button.Y.Should().BeGreaterThan(bubble.Bounds.Bottom);
                button.X.Should().BeGreaterThanOrEqualTo(0d);
                cancel.Classes.Should().Contain("danger");
                GenerationAttachmentStripView strip = message.GetVisualDescendants().OfType<GenerationAttachmentStripView>().Single();
                Point stripOrigin = strip.TranslatePoint(default, message) ?? throw new InvalidOperationException();
                stripOrigin.X.Should().BeApproximately(origin.X, 1d);
                Point bubbleBottom = bubble.TranslatePoint(new Point(0d, bubble.Bounds.Height), message)
                    ?? throw new InvalidOperationException();
                (bubbleBottom.Y - stripOrigin.Y - strip.Bounds.Height).Should().BeApproximately(editor.Padding.Bottom, 1d);
                GenerationChatViewModelTests.SaveChatFrame(window, $"short-editor-{assistant}.png");

                model.CancelCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.EditorProgress == 0d);
                editor.Bounds.Width.Should().BeApproximately(width, 1d);
                (presenter.TranslatePoint(default, message) ?? throw new InvalidOperationException()).Should().Be(origin);
                Point selectionEnd = editor.TranslatePoint(new Point(editor.Bounds.Width - 1d, editor.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException();
                Point selectionStart = editor.TranslatePoint(new Point(0d, editor.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException();
                window.MouseDown(selectionEnd, MouseButton.Left);
                window.MouseMove(selectionStart, RawInputModifiers.LeftMouseButton);
                window.MouseUp(selectionStart, MouseButton.Left);
                editor.SelectedText.Should().Be(saved.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithImagePaste_AddsOnlyToDraftAndCancelKeepsExistingPreview()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationReference reference = new("old", "old.png", "image/png");
            GenerationChatMessage saved = new(false, "Original", new DateTime(2026, 11, 24), new[] { reference });
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(
                new GenerationChatHistory(new[] { saved }, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                GenerationChatMessageViewModel model = context.Chat.Messages.Single();
                AttachedImageViewModel original = model.AttachedImages.Single();
                int resets = 0;
                ((INotifyCollectionChanged)model.AttachedImages).CollectionChanged += (_, args) =>
                {
                    resets += args.Action == NotifyCollectionChangedAction.Reset ? 1 : 0;
                };
                AttachedImageDto pasted = new("paste.png", "image/png", GalleryThumbnailTestImages.CreatePngBytes(32, 32));
                Mock<IClipboardImageService> clipboard = new();
                clipboard.Setup(service => service.TryGetImageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => ImageAttachmentInput.FromImage(pasted));
                ClipboardPasteBehavior.SetClipboardImageService(view, clipboard.Object);
                GenerationChatMessageView row = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Single();
                TextBox editor = row.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                model.EditCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => row.EditorProgress == 1d);
                editor.Focus();

                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => model.Images.Count == 2);

                model.Snapshot().Images.Should().Equal(reference);
                model.AttachedImages[0].Should().BeSameAs(original);
                model.SaveCommand.CanExecute(null).Should().BeTrue();
                model.CancelCommand.Execute(null);
                model.Images.Should().ContainSingle();
                model.AttachedImages.Single().Should().BeSameAs(original);
                resets.Should().Be(0);
                context.Chats.Verify(store => store.SaveAsync(It.IsAny<Guid>(), It.IsAny<GenerationChatHistory>(),
                    It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
