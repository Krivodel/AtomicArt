using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowInGalleryButton_WhenClicked_CollapsesChatAndRevealsItsCard(bool generating)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreSourceAsync();
            await context.Chat.OpenCommand.ExecuteAsync(source);
            GenerationChatBatch? batch = generating ? await SubmitParallelAsync(context.Chat) : null;
            GenerationChatMessageViewModel[] messages = context.Chat.Messages.ToArray();
            bool collapsedBeforeReveal = false;
            context.GalleryOperations.Setup(operations => operations.RevealAsync(source.Id, It.IsAny<CancellationToken>()))
                .Returns((Guid _, CancellationToken _) =>
                {
                    collapsedBeforeReveal = !context.Chat.IsExpanded;

                    return Task.CompletedTask;
                });
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 640d);

            try
            {
                await WaitForFramesAsync(window, () => view.IsPresented);
                Button reveal = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, context.Chat.ShowInGalleryCommand));
                Button branches = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, context.Chat.OpenBranchMapCommand));
                Button close = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, context.Chat.CollapseCommand));
                Border chrome = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-history"));
                Point closeOrigin = close.TranslatePoint(default, chrome)
                    ?? throw new InvalidOperationException("The close button has no position.");
                closeOrigin.Y.Should().BeApproximately(chrome.Bounds.Width - closeOrigin.X - close.Bounds.Width, 0.1d);
                PathIcon icon = reveal.Content.Should().BeOfType<PathIcon>().Subject;
                icon.Data.Should().NotBeNull();
                icon.Bounds.Width.Should().BeGreaterThan(0d);
                reveal.IsEffectivelyVisible.Should().BeTrue();
                reveal.IsEnabled.Should().BeTrue();
                Grid.GetColumn(reveal).Should().BeLessThan(Grid.GetColumn(branches));
                Point center = reveal.TranslatePoint(new Point(reveal.Bounds.Width / 2d, reveal.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The show-in-gallery button has no position.");

                window.MouseDown(center, MouseButton.Left);
                window.MouseUp(center, MouseButton.Left);
                await (context.Chat.ShowInGalleryCommand.ExecutionTask
                    ?? throw new InvalidOperationException("The show-in-gallery command did not run."));

                collapsedBeforeReveal.Should().BeTrue();
                context.Chat.IsExpanded.Should().BeFalse();
                context.Chat.HasChat.Should().BeTrue();
                context.Chat.Messages.Should().Equal(messages);
                context.GalleryOperations.Verify(operations => operations.RevealAsync(source.Id, It.IsAny<CancellationToken>()), Times.Once);
                batch?.Runs.Should().OnlyContain(run => !run.Completion.IsCompleted);
            }
            finally
            {
                window.Close();

                if (batch is not null)
                {
                    foreach (GenerationChatRun run in batch.Runs)
                    {
                        run.Complete(null);
                    }
                }
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ChatToolbar_WhenHovered_BlocksTheContentBehindItsButtonsAndEdges(bool topEdge, bool rightEdge)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatMessage[] messages = Enumerable.Range(0, 40)
                .Select(index => new GenerationChatMessage(false, $"Message {index}",
                    new DateTime(2026, 11, 25), Array.Empty<GenerationReference>())).ToArray();
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(messages, includesCardTurn: true));
            await context.Chat.OpenCommand.ExecuteAsync(source);
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = Show(view, 560d, 360d);

            try
            {
                await WaitForFramesAsync(window, () => view.IsPresented);
                Button close = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => object.ReferenceEquals(button.Command, context.Chat.CollapseCommand));
                Border toolbar = close.GetVisualAncestors().OfType<Border>().First();
                Border chrome = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-history"));
                Point toolbarOrigin = toolbar.TranslatePoint(default, chrome)
                    ?? throw new InvalidOperationException("The toolbar has no chat position.");
                toolbarOrigin.Y.Should().BeApproximately(0d, 0.1d);
                (toolbarOrigin.X + toolbar.Bounds.Width).Should().BeApproximately(chrome.Bounds.Width, 0.1d);
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                Border bubble = view.GetVisualDescendants().OfType<Border>()
                    .First(border => border.Classes.Contains("generation-chat-bubble"));
                Point buttonInset = new(1d, 1d);
                Point toolbarPoint = rightEdge
                    ? new Point(toolbar.Bounds.Width - buttonInset.X, toolbar.Bounds.Height / 2d)
                    : topEdge ? new Point(toolbar.Bounds.Width / 2d, buttonInset.Y)
                    : close.TranslatePoint(buttonInset, toolbar) ?? throw new InvalidOperationException("The button has no toolbar position.");
                Point buttonPoint = toolbar.TranslatePoint(toolbarPoint, scroll)
                    ?? throw new InvalidOperationException("The close button has no scroll position.");
                Point bubbleOrigin = bubble.TranslatePoint(default, scroll)
                    ?? throw new InvalidOperationException("The message has no scroll position.");
                scroll.Offset = new Vector(0d, scroll.Offset.Y + bubbleOrigin.Y - buttonPoint.Y + (bubble.Bounds.Height / 2d));
                window.CaptureRenderedFrame()?.Dispose();
                Point pointer = toolbar.TranslatePoint(toolbarPoint, window)
                    ?? throw new InvalidOperationException("The toolbar has no window position.");
                toolbar.IsVisible = false;
                window.CaptureRenderedFrame()?.Dispose();
                window.MouseMove(pointer);

                if (!rightEdge)
                {
                    bubble.IsPointerOver.Should().BeTrue();
                }

                toolbar.IsVisible = true;
                window.CaptureRenderedFrame()?.Dispose();
                Point protectedPoint = pointer + new Vector(rightEdge ? -buttonInset.X : buttonInset.X, 0d);
                window.MouseMove(protectedPoint);

                bubble.IsPointerOver.Should().BeFalse();
                Visual hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(protectedPoint));
                hit.GetSelfAndVisualAncestors().Should().Contain(toolbar);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ShowInGalleryCommand_WhenSourceIsDeleted_DisablesTheAction()
    {
        using ChatContext context = new();
        context.Chat.ShowInGalleryCommand.CanExecute(null).Should().BeFalse();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        context.Chat.ShowInGalleryCommand.CanExecute(null).Should().BeTrue();

        await context.Gallery.DeleteOrCancelCommand.ExecuteAsync(source);
        await context.Chat.ShowInGalleryCommand.ExecuteAsync(null);

        context.Chat.ShowInGalleryCommand.CanExecute(null).Should().BeFalse();
        context.GalleryOperations.Verify(operations => operations.RevealAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
