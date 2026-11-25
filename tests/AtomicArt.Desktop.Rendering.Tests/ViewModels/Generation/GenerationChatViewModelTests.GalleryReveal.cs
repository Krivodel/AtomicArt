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
