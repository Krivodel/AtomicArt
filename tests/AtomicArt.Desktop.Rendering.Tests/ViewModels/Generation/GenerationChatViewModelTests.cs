using System.ComponentModel;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests : DesktopControlTestBase
{
    public new static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DesktopControlTestApplication>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public async Task ChatView_WithPreviewStillLoading_WaitsBeforeShowingHistory()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            TaskCompletionSource decoding = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource allowPreview = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
            GenerationChatResult result = context.CreateResult("A square image");
            GenerationItemViewModel source = await context.RestoreLongHistoryAsync(result);
            GenerationChatView view = new()
            {
                PreviewLoader = context.CreateDelayedPreviewLoader(decoding, allowPreview.Task),
                DataContext = context.Chat
            };
            view.PropertyChanged += (_, args) =>
            {
                if (args.Property == GenerationChatView.IsPresentedProperty && view.IsPresented)
                {
                    presented.TrySetResult();
                }
            };
            Window window = DesktopControlTestBase.Show(view, 560d, 300d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await decoding.Task.WaitAsync(TimeSpan.FromSeconds(5));
                view.IsPresented.Should().BeFalse();
                context.VerifyOriginalsWereNotLoaded();

                allowPreview.SetResult();
                await presented.Task.WaitAsync(TimeSpan.FromSeconds(5));
                window.CaptureRenderedFrame();
                GenerationReferencePreviewControl olderPreview = view.GetVisualDescendants()
                    .OfType<GenerationReferencePreviewControl>().First();
                olderPreview.IsNearViewport.Should().BeFalse();
                olderPreview.Source.Should().BeNull();
                GenerationChatImageFrame frame = view.GetVisualDescendants()
                    .OfType<GenerationChatImageFrame>().Last();
                frame.Bounds.Width.Should().BeApproximately(frame.Bounds.Height, 1d);

                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                StackPanel footer = scroll.GetVisualDescendants().OfType<StackPanel>()
                    .Last(panel => panel.Classes.Contains("generation-chat-tools"));
                Point? bottom = footer.TranslatePoint(new Point(0d, footer.Bounds.Height), scroll);
                bottom.Should().NotBeNull();
                bottom.GetValueOrDefault().Y.Should().BeLessThanOrEqualTo(scroll.Bounds.Height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpenCommand_WithCodexCard_PreservesPanelReferences()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();

        await context.Chat.OpenCommand.ExecuteAsync(source);

        context.Panel.Verify(panel => panel.TryApplyPresetAsync(
            It.Is<GenerationPanelPreset>(preset => preset.ModelId == source.ModelId && preset.Prompt == null && preset.References == null),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Chat.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task OpenCommand_WithPanelDraft_PreservesPromptAndAttachments()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        LocalCodexSettings settings = new();
        using UniversalNanoBananaPanelViewModel panel =
            AtomicArt.Desktop.Tests.ViewModels.Generation.UniversalNanoBananaPanelViewModelTests.CreateViewModel(
                imageModelOptionCatalog: new ImageModelOptionCatalog(settings), localCodexSettings: settings);
        context.Chat.Connect(context.Gallery, panel);
        panel.ConnectChat(context.Chat);
        const string Draft = "Keep this draft\nwith its second line";
        panel.Prompt = Draft;
        await panel.AttachImagesCommand.ExecuteAsync(new AttachedImageDto[]
        {
            GenerationImageTestData.CreateAttachedImage("draft.png")
        });
        AttachedImageViewModel attachment = panel.AttachedImages.Single();

        await context.Chat.OpenCommand.ExecuteAsync(source);

        panel.SelectedModel?.Id.Should().Be(source.ModelId);
        panel.Prompt.Should().Be(Draft);
        panel.AttachedImages.Should().ContainSingle().Which.Should().BeSameAs(attachment);
        context.Chat.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task OpenCommand_WithContinuationPending_OpensTheOriginalAndKeepsTheGroupAccessible()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        int originalCount = context.Chat.Messages.Count;
        GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        context.Chat.Collapse();

        context.Chat.ToggleCommand.Execute(null);
        context.Chat.Messages[^1].HasPendingResults.Should().BeTrue();
        context.Chat.Collapse();
        await context.Chat.OpenCommand.ExecuteAsync(source);

        context.Chat.Messages.Should().HaveCount(originalCount);
        context.Chat.Messages.Should().OnlyContain(message => !message.HasPendingResults);
        GenerationChatGroupViewModel group = context.Chat.Messages[^1].Groups.Should().ContainSingle().Which;
        await context.Chat.OpenGroupCommand.ExecuteAsync(group);

        context.Chat.Messages.Should().HaveCount(originalCount + 2);
        context.Chat.Messages[^1].HasPendingResults.Should().BeTrue();
        context.Chat.CanSubmit.Should().BeTrue();
        context.Panel.Verify(panel => panel.TryApplyPresetAsync(
            It.IsAny<GenerationPanelPreset>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        batch.Runs.Should().OnlyContain(run => !run.Completion.IsCompleted);

        foreach (GenerationChatRun run in batch.Runs)
        {
            run.Complete(null);
        }
    }

    [Fact]
    public async Task ChatView_WithNewParallelReply_AnimatesWithoutAnEmptyChoiceRow()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreSourceAsync();
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 640d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
                GenerationChatMessageViewModel reply = context.Chat.Messages[^1];
                bool sawIntermediate = false;

                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                {
                    Grid? bubble = view.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid =>
                        object.ReferenceEquals(grid.DataContext, reply) && grid.Classes.Contains("generation-chat-message"));

                    if (bubble is null)
                    {
                        return false;
                    }

                    sawIntermediate |= bubble.Opacity is > 0d and < 1d;

                    return sawIntermediate && bubble.Opacity == 1d;
                });

                GenerationChatReveal[] choiceRows = view.GetVisualDescendants().OfType<GenerationChatReveal>()
                    .Where(border => border.Classes.Contains("generation-chat-choice")).ToArray();
                choiceRows.Should().HaveCount(2).And.OnlyContain(row => !row.IsVisible && row.Bounds.Height == 0d);

                foreach (GenerationChatRun run in batch.Runs)
                {
                    run.Complete(null);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ChatView_WithEditAndFinish_PreservesTextOriginFromFirstFrame(bool lastMessage, bool save)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreLongHistoryAsync(result: null);
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();

                if (!lastMessage)
                {
                    scroll.Offset = new Vector(0d, scroll.ScrollBarMaximum.Y / 2d);
                }

                window.CaptureRenderedFrame()?.Dispose();
                GenerationChatMessageView message = lastMessage
                    ? view.GetVisualDescendants().OfType<GenerationChatMessageView>().Last()
                    : scroll.GetVisualDescendants().OfType<GenerationChatMessageView>().First(candidate =>
                        candidate.TranslatePoint(default, scroll)?.Y is > 25d and < 170d);
                GenerationChatMessageViewModel model = Assert.IsType<GenerationChatMessageViewModel>(message.DataContext);
                TextBox editor = message.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                TextPresenter presenter = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
                Point origin = presenter.TranslatePoint(default, scroll) ?? throw new InvalidOperationException();
                double offsetBeforeEdit = scroll.Offset.Y;
                double previousHeight = message.Bounds.Height;

                model.EditCommand.Execute(null);
                window.UpdateLayout();
                (presenter.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y.Should().BeApproximately(origin.Y, 1d);

                Point first = presenter.TranslatePoint(default, scroll) ?? throw new InvalidOperationException();
                first.X.Should().BeApproximately(origin.X, 1d);
                double animatedHeight = message.Bounds.Height - previousHeight;
                first.Y.Should().BeApproximately(origin.Y - (lastMessage ? animatedHeight : 0d), 1d);
                GenerationAttachmentStripView strip = message.GetVisualDescendants().OfType<GenerationAttachmentStripView>().Single();
                double referenceGrowth = strip.DesiredSize.Height * message.ReferenceRevealProgress;
                GenerationChatMessagePanel layout = message.GetVisualDescendants().OfType<GenerationChatMessagePanel>().Single();
                double footerGrowth = Math.Max(0d, layout.Children[2].DesiredSize.Height - layout.Children[1].DesiredSize.Height)
                    * message.EditorProgress;
                animatedHeight.Should().BeApproximately(referenceGrowth + footerGrowth, 1d);
                double previousProgress = message.ReferenceRevealProgress;

                for (int frame = 0; frame < 30; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame()?.Dispose();
                    message.ReferenceRevealProgress.Should().BeGreaterThanOrEqualTo(previousProgress);
                    double expectedOffset = lastMessage
                        ? offsetBeforeEdit + message.Bounds.Height - previousHeight
                        : offsetBeforeEdit;
                    scroll.Offset.Y.Should().BeApproximately(expectedOffset, 1d);
                    (presenter.TranslatePoint(default, message) ?? throw new InvalidOperationException()).X.Should().BeApproximately(
                        origin.X - ((message.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).X), 1d);
                    previousProgress = message.ReferenceRevealProgress;
                }

                foreach (Button action in layout.Children[2].GetVisualDescendants().OfType<Button>())
                {
                    TextBlock label = action.GetVisualDescendants().OfType<TextBlock>()
                        .First(block => !string.IsNullOrEmpty(block.Text));
                    Point labelTop = label.TranslatePoint(default, action) ?? throw new InvalidOperationException();
                    labelTop.Y.Should().BeGreaterThanOrEqualTo(0d);
                    (labelTop.Y + label.Bounds.Height).Should().BeLessThanOrEqualTo(action.Bounds.Height);
                }

                if (model.IsAssistant)
                {
                    Button[] actions = layout.Children[2].GetVisualDescendants().OfType<Button>().ToArray();
                    (actions[0].TranslatePoint(default, layout) ?? throw new InvalidOperationException()).Y.Should().BeApproximately(
                        (actions[1].TranslatePoint(default, layout) ?? throw new InvalidOperationException()).Y, 1d);
                }

                GenerationChatViewModelTests.SaveChatFrame(window, $"editing-{lastMessage}-{save}.png");
                model.EditText = string.Join(" ", Enumerable.Repeat("A longer edited message", 12));

                for (int frame = 0; frame < 24; frame++)
                {
                    window.CaptureRenderedFrame()?.Dispose();
                    await Task.Delay(16);
                }

                double expanded = message.Bounds.Height;
                bool sawClosing = false;
                void TrackClosing(object? sender, AvaloniaPropertyChangedEventArgs args)
                {
                    if (args.Property == GenerationChatMessageView.EditorProgressProperty)
                    {
                        sawClosing |= message.EditorProgress is > 0d and < 1d;
                    }
                }
                message.PropertyChanged += TrackClosing;
                Button finish = message.GetVisualDescendants().OfType<Button>().Single(button =>
                    ReferenceEquals(button.Command, save ? model.SaveCommand : model.CancelCommand));
                Point finishPoint = finish.TranslatePoint(new Point(finish.Bounds.Width / 2d, finish.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException();
                window.MouseMove(finishPoint, RawInputModifiers.None);
                window.MouseDown(finishPoint, MouseButton.Left);
                window.MouseUp(finishPoint, MouseButton.Left);

                if (save)
                {
                    await (model.SaveCommand.ExecutionTask ?? Task.CompletedTask);
                }

                window.UpdateLayout();
                model.IsEditing.Should().BeFalse();
                message.IsEditorPresented.Should().BeTrue();
                message.Bounds.Height.Should().BeLessThanOrEqualTo(expanded);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.EditorProgress == 0d);
                message.PropertyChanged -= TrackClosing;
                sawClosing.Should().BeTrue();
                model.IsEditing.Should().BeFalse();
                message.IsEditorPresented.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_AfterCopyAndPointerExit_HidesActionsAndResetsCheckmark()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 640d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                GenerationChatMessageView message = view.GetVisualDescendants().OfType<GenerationChatMessageView>().First();
                GenerationChatMessageViewModel model = Assert.IsType<GenerationChatMessageViewModel>(message.DataContext);
                TextBox text = message.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                Point over = text.TranslatePoint(new Point(10d, 10d), window) ?? throw new InvalidOperationException();
                window.MouseMove(over, RawInputModifiers.None);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.ToolsRevealProgress == 1d);
                Button copy = message.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, model.CopyCommand));
                Point point = copy.TranslatePoint(new Point(copy.Bounds.Width / 2d, copy.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException();
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                await WaitForFramesAsync(window, () => model.IsCopied);
                copy.GetVisualDescendants().OfType<PathIcon>().Single().IsVisible.Should().BeTrue();

                window.MouseMove(new Point(2d, 2d), RawInputModifiers.None);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => message.ToolsRevealProgress == 0d);

                model.IsCopied.Should().BeFalse();
                StackPanel tools = message.GetVisualDescendants().OfType<StackPanel>()
                    .Single(panel => panel.Classes.Contains("generation-chat-tools"));
                tools.Opacity.Should().Be(0d);
                tools.IsHitTestVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithReadyParallelVariant_ShowsAccentChoiceUnderImageWithoutOverlap()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(new TaskCompletionSource(), Task.CompletedTask);
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, 560d, 820d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.GetVisualDescendants().OfType<GenerationChatMessageView>()
                    .Last().ArrivalProgress == 1d);
                GenerationChatResult result = context.AddResultCard(batch, "A completed variant");
                await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[0].Complete(result));
                GenerationChatMessageView reply = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Last();
                TextBox text = reply.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                window.CaptureRenderedFrame()?.Dispose();
                Point caption = text.TranslatePoint(new Point(10d, 10d), window) ?? throw new InvalidOperationException();

                window.MouseMove(caption, RawInputModifiers.None);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => reply.ToolsRevealProgress == 1d);

                GenerationChatReveal[] choices = reply.GetVisualDescendants().OfType<GenerationChatReveal>()
                    .Where(reveal => reveal.Classes.Contains("generation-chat-choice")).ToArray();
                choices[0].IsVisible.Should().BeTrue();
                choices[1].IsVisible.Should().BeFalse();
                Button choose = choices[0].GetVisualDescendants().OfType<Button>().Single();
                GenerationChatImageFrame image = reply.GetVisualDescendants().OfType<GenerationChatImageFrame>().First();
                StackPanel tools = reply.GetVisualDescendants().OfType<StackPanel>()
                    .Single(panel => panel.Classes.Contains("generation-chat-tools"));
                double imageBottom = (image.TranslatePoint(new Point(0d, image.Bounds.Height), reply) ?? throw new InvalidOperationException()).Y;
                double choiceTop = (choose.TranslatePoint(default, reply) ?? throw new InvalidOperationException()).Y;
                double choiceBottom = (choose.TranslatePoint(new Point(0d, choose.Bounds.Height), reply) ?? throw new InvalidOperationException()).Y;
                double toolsTop = (tools.TranslatePoint(default, reply) ?? throw new InvalidOperationException()).Y;
                choiceTop.Should().BeGreaterThanOrEqualTo(imageBottom);
                choose.Content.Should().Be("Go to this chat");
                choose.IsEnabled.Should().BeTrue();
                choose.Bounds.Height.Should().BeGreaterThan(25d);
                choiceBottom.Should().BeLessThanOrEqualTo(toolsTop);
                choose.Background.Should().BeSameAs(view.FindResource("MetadataRepeatBrush"));
                choose.Bounds.Width.Should().BeGreaterThan(180d);
                Point bottom = (choose.TranslatePoint(new Point(0d, choose.Bounds.Height), window) ?? throw new InvalidOperationException());
                bottom.Y.Should().BeLessThan(window.Bounds.Height);
                GenerationChatViewModelTests.SaveChatFrame(window, "parallel-choice.png");
                batch.Runs[1].Complete(null);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithControlWheel_AdjustsChatTextWithoutScrolling()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 560d, 640d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreLongHistoryAsync(result: null));
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                double offset = scroll.Offset.Y;
                TextBox text = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Last()
                    .FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                Point over = text.TranslatePoint(new Point(10d, 10d), window) ?? throw new InvalidOperationException();
                window.MouseWheel(over, new Vector(0d, 1d), RawInputModifiers.Control);
                await (context.Chat.AdjustTextSizeCommand.ExecutionTask ?? Task.CompletedTask);

                context.TextSizes.Verify(controller => controller.AdjustAsync(TextSizeAdjustment.Increase,
                    It.IsAny<CancellationToken>()), Times.Once);
                scroll.Offset.Y.Should().Be(offset);
                context.TextSizes.SetupGet(controller => controller.CurrentTextSize).Returns(18d);
                context.TextSizes.Raise(controller => controller.TextSizeChanged += null, EventArgs.Empty);
                window.CaptureRenderedFrame()?.Dispose();
                text.FontSize.Should().Be(18d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(360, false)]
    [InlineData(900, false)]
    [InlineData(360, true)]
    [InlineData(900, true)]
    public async Task ChatView_WithUserAndAssistantMessages_AlignsRepliesLeftAndPromptsRight(
        int windowWidth, bool withImage)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(
                new TaskCompletionSource(), Task.CompletedTask, 320, 320);
            GenerationChatResult result = withImage
                ? context.CreateResult("Assistant reply")
                : new GenerationChatResult("Assistant reply", null);
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, windowWidth, 850d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync(result));
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                ItemsControl items = view.FindControl<ItemsControl>("ChatItems")
                    ?? throw new InvalidOperationException("The chat message list is unavailable.");
                ItemsPresenter presenter = items.GetVisualDescendants().OfType<ItemsPresenter>().First();
                GenerationChatMessageView[] messages = view.GetVisualDescendants()
                    .OfType<GenerationChatMessageView>().ToArray();
                GenerationChatMessageView prompt = messages.Single(message =>
                    message.DataContext is GenerationChatMessageViewModel { IsAssistant: false });
                GenerationChatMessageView reply = messages.Single(message =>
                    message.DataContext is GenerationChatMessageViewModel { IsAssistant: true });
                Border promptBubble = prompt.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-bubble"));
                Border replyBubble = reply.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-bubble"));
                Point promptOrigin = promptBubble.TranslatePoint(default, presenter)
                    ?? throw new InvalidOperationException("The prompt bubble has no message-list position.");
                Point replyOrigin = replyBubble.TranslatePoint(default, presenter)
                    ?? throw new InvalidOperationException("The reply bubble has no message-list position.");
                GenerationChatViewModelTests.SaveChatFrame(window, $"message-alignment-{windowWidth}-{withImage}.png");

                replyOrigin.X.Should().BeApproximately(0d, 1d);
                (promptOrigin.X + promptBubble.Bounds.Width).Should().BeApproximately(presenter.Bounds.Width, 1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WhenExpanded_RendersShadowOutsideItsContentBounds()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationChatView view = new() { DataContext = context.Chat, Margin = new Thickness(70d) };
            Grid host = new() { Background = Avalonia.Media.Brushes.SlateGray };
            host.Children.Add(view);
            Window window = DesktopControlTestBase.Show(host, 800d, 650d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(
                    await context.RestoreSourceAsync(new GenerationChatResult("Assistant reply", null)));
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                Border chrome = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-history"));
                Point origin = chrome.TranslatePoint(default, window)
                    ?? throw new InvalidOperationException("The chat surface has no window position.");
                Rect bounds = new(origin, chrome.Bounds.Size);
                using SKBitmap shadowed = CaptureRenderedBitmap(window);
                GenerationChatViewModelTests.SaveChatFrame(window, "chat-shadow.png");

                chrome.SetCurrentValue(Border.BoxShadowProperty, default);
                window.CaptureRenderedFrame()?.Dispose();
                using SKBitmap flat = CaptureRenderedBitmap(window);
                string clipping = string.Join(", ", chrome.GetVisualAncestors().Select(ancestor =>
                    $"{ancestor.GetType().Name}: ClipToBounds={ancestor.ClipToBounds}, Bounds={ancestor.Bounds}"));

                chrome.Bounds.Size.Should().Be(bounds.Size);
                GenerationChatViewModelTests.CountDarkenedPixels(shadowed, flat,
                    new Rect(bounds.Left - 30d, bounds.Top + 30d, 28d, bounds.Height - 60d)).Should().BeGreaterThan(10, clipping);
                GenerationChatViewModelTests.CountDarkenedPixels(shadowed, flat,
                    new Rect(bounds.Right + 2d, bounds.Top + 30d, 28d, bounds.Height - 60d)).Should().BeGreaterThan(10);
                GenerationChatViewModelTests.CountDarkenedPixels(shadowed, flat,
                    new Rect(bounds.Left + 30d, bounds.Bottom + 2d, bounds.Width - 60d, 28d)).Should().BeGreaterThan(10);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(320, 320)]
    [InlineData(640, 360)]
    [InlineData(360, 640)]
    [InlineData(840, 360)]
    public async Task ChatView_WithResultImage_FillsBubbleWidthWithoutCropping(int width, int height)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(
                new TaskCompletionSource(), Task.CompletedTask, width, height);
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, 560d, 1020d);

            try
            {
                GenerationItemViewModel source = await context.RestoreSourceAsync(context.CreateResult("Image caption\n\n"));
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                window.CaptureRenderedFrame()?.Dispose();
                GenerationChatMessageView reply = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Last();
                GenerationChatImageFrame image = reply.GetVisualDescendants().OfType<GenerationChatImageFrame>().Single();
                Border bubble = reply.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-bubble"));
                TextBox editor = reply.FindControl<TextBox>("MessageEditor") ?? throw new InvalidOperationException();
                editor.Text.Should().Be("Image caption\n\n");
                editor.GetVisualDescendants().OfType<TextPresenter>().Single().TextLayout.TextLines.Should().HaveCount(3);

                GenerationReferencePreviewControl preview = image.GetVisualDescendants().OfType<GenerationReferencePreviewControl>().Single();
                preview.Loader.Should().BeSameAs(loader);
                preview.Reference.Should().NotBeNull();
                preview.IsNearViewport.Should().BeTrue();
                await preview.PreviewLoad;
                window.CaptureRenderedFrame()?.Dispose();
                preview.Source.Should().NotBeNull();
                image.Preview.Should().BeSameAs(preview.Source);
                image.Preview?.Size.AspectRatio.Should().BeApproximately((double)width / height, 0.01d);
                image.Bounds.Width.Should().BeApproximately(bubble.Bounds.Width, 1d);
                image.Bounds.Height.Should().BeApproximately(image.Bounds.Width * height / width, 1d);
                (image.TranslatePoint(default, bubble) ?? throw new InvalidOperationException()).X.Should().BeApproximately(0d, 1d);
                (image.TranslatePoint(new Point(0d, image.Bounds.Height), bubble) ?? throw new InvalidOperationException()).Y
                    .Should().BeApproximately(bubble.Bounds.Height, 1d);
                GenerationChatViewModelTests.SaveChatFrame(window, $"image-{width}x{height}.png");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpenCommand_WithSavedChat_LoadsHistoryOnDemandAndReusesLoadedHistory()
    {
        using ChatContext context = new();
        GenerationChatHistory history = new(new GenerationChatMessage[]
            { new GenerationChatMessage(false, "Saved dialogue", new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), Array.Empty<GenerationReference>()) }, true);
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(history);
        context.Chats.Verify(store => store.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        await context.Chat.OpenCommand.ExecuteAsync(source);
        context.Chat.Collapse();
        await context.Chat.OpenCommand.ExecuteAsync(source);

        context.Chat.Messages.Single().Text.Should().Be("Saved dialogue");
        context.Chats.Verify(store => store.LoadAsync(history.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReferencePreviewLoader_WithRevisitedImage_ReusesDecodedBitmap()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(
                new TaskCompletionSource(), Task.CompletedTask);
            GenerationReference reference = context.CreateResult("Square").Image ?? throw new InvalidOperationException("The square result has no image reference.");
            Bitmap decoded;

            using (GalleryPreviewBitmapLease first =
                Assert.IsType<GalleryPreviewBitmapLease>(await loader.AcquireAsync(reference, CancellationToken.None, 960)))
            {
                decoded = first.Bitmap;
            }

            using GalleryPreviewBitmapLease second =
                Assert.IsType<GalleryPreviewBitmapLease>(await loader.AcquireAsync(reference, CancellationToken.None, 960));

            second.Bitmap.Should().BeSameAs(decoded);
        });
    }

    [Fact]
    public async Task ChatView_WithVisibleMessageEditing_RevealsReferencesWithoutJumping()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreLongHistoryAsync(result: null);
            GenerationChatView view = new() { DataContext = context.Chat };
            TaskCompletionSource presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
            view.PropertyChanged += (_, args) =>
            {
                if (args.Property == GenerationChatView.IsPresentedProperty && view.IsPresented)
                {
                    presented.TrySetResult();
                }
            };
            Window window = DesktopControlTestBase.Show(view, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await presented.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Border history = view.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-history"));

                for (int frame = 0; frame < 120 && history.Opacity < 1d; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame();
                }

                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Vector(0d, scroll.ScrollBarMaximum.Y / 2d);
                window.CaptureRenderedFrame();
                GenerationChatMessageView message = scroll.GetVisualDescendants()
                    .OfType<GenerationChatMessageView>().First(candidate =>
                        candidate.TranslatePoint(default, scroll)?.Y is > 25d and < 170d);
                double top = (message.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y;
                double offset = scroll.Offset.Y;
                bool sawIntermediate = false;
                TaskCompletionSource expanded = new(TaskCreationOptions.RunContinuationsAsynchronously);
                message.PropertyChanged += (_, args) =>
                {
                    if (args.Property == GenerationChatMessageView.ReferenceRevealProgressProperty)
                    {
                        sawIntermediate |= message.ReferenceRevealProgress is > 0d and < 1d;

                        if (message.ReferenceRevealProgress == 1d)
                        {
                            expanded.TrySetResult();
                        }
                    }
                };

                GenerationChatMessageViewModel edited = Assert.IsType<GenerationChatMessageViewModel>(message.DataContext);
                edited.EditCommand.Execute(null);
                edited.IsEditing.Should().BeTrue();
                message.IsEditing.Should().BeTrue("the active message starts editing before the next layout");

                for (int frame = 0; frame < 120 && !expanded.Task.IsCompleted; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame();
                    object.ReferenceEquals(message.DataContext, edited).Should().BeTrue(
                        "the edited row must remain realized during animation, current offset {0}, initial offset {1}",
                        scroll.Offset.Y, offset);
                }

                message.IsEditing.Should().BeTrue();
                message.ReferenceRevealProgress.Should().Be(1d);
                window.CaptureRenderedFrame();

                sawIntermediate.Should().BeTrue();
                (message.TranslatePoint(default, scroll) ?? throw new InvalidOperationException()).Y.Should().BeApproximately(top, 1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChooseVariantCommand_WithParallelResults_ContinuesFromSelectedCard(bool otherResultPending)
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel groupedReply = context.Chat.Messages[^1];
        GenerationChatImageViewModel ready = groupedReply.Images[0];
        GenerationChatImageViewModel pending = groupedReply.Images[1];
        GenerationChatResult result = context.AddResultCard(batch, "First variant");

        await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[0].Complete(result));

        if (!otherResultPending)
        {
            await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[1].Complete(null));
        }

        context.Chat.IsBusy.Should().BeTrue();
        ready.CanChoose.Should().BeTrue();
        pending.CanChoose.Should().BeFalse();

        await context.Chat.ChooseVariantCommand.ExecuteAsync(ready);

        context.Chat.IsBusy.Should().BeFalse();
        context.Chat.Messages[^1].Text.Should().Be(result.Text);
        context.Chat.Messages[^1].Images.Should().ContainSingle().Which.Reference.Should().Be(result.Image);
        context.Panel.Verify(panel => panel.TryApplyPresetAsync(
            It.IsAny<GenerationPanelPreset>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        if (otherResultPending)
        {
            batch.Runs[1].Complete(null);
        }
    }

    [Fact]
    public async Task SubmitAsync_WithAllVariantsFailed_AllowsAnotherGeneration()
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);

        await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[0].Complete(null));
        await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[1].Complete(null));

        context.Chat.IsBusy.Should().BeFalse();
        context.Chat.Messages[^1].IsVariantChoice.Should().BeTrue();
        context.Chat.CanSubmit.Should().BeTrue();
    }

    private static int CountDarkenedPixels(SKBitmap shadowed, SKBitmap flat, Rect region)
    {
        const int MinimumDifference = 2;
        int count = 0;

        for (int y = Math.Max(0, (int)region.Top); y < Math.Min(shadowed.Height, region.Bottom); y++)
        {
            for (int x = Math.Max(0, (int)region.Left); x < Math.Min(shadowed.Width, region.Right); x++)
            {
                if (flat.GetPixel(x, y).Red - shadowed.GetPixel(x, y).Red >= MinimumDifference)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void SaveChatFrame(Window window, string name)
    {
        using Bitmap frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The chat frame was not captured.");
        GenerationChatViewModelTests.SaveRenderedFrame(frame, name);
        using MemoryStream stream = new();
        frame.Save(stream);
        stream.Position = 0;
        using SKBitmap bitmap = SKBitmap.Decode(stream);
        TextPresenter? text = window.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault(presenter =>
        {
            Point? point = presenter.TranslatePoint(default, window);

            return presenter.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(presenter.Text)
                && presenter.GetVisualAncestors().OfType<GenerationChatMessageView>().Any()
                && point is { X: >= 0d, Y: >= 0d } position
                && position.Y + presenter.Bounds.Height < window.Bounds.Height;
        });
        text.Should().NotBeNull();

        if (text is not null && text.TranslatePoint(default, window) is { } origin)
        {
            int lightPixels = 0;

            for (int y = Math.Max(0, (int)origin.Y); y < Math.Min(bitmap.Height, origin.Y + text.Bounds.Height); y++)
            {
                for (int x = Math.Max(0, (int)origin.X); x < Math.Min(bitmap.Width, origin.X + text.Bounds.Width); x++)
                {
                    SKColor pixel = bitmap.GetPixel(x, y);

                    if (pixel.Red > 160 && pixel.Green > 160 && pixel.Blue > 160)
                    {
                        lightPixels++;
                    }
                }
            }

            lightPixels.Should().BeGreaterThan(8, "the captured chat must contain visible message text");
        }

    }

    private static async Task WaitForFramesAsync(Window window, Func<bool> condition)
    {
        for (int frame = 0; frame < 120; frame++)
        {
            window.CaptureRenderedFrame()?.Dispose();

            if (condition())
            {
                return;
            }

            await Task.Delay(16);
        }

        throw new TimeoutException("The expected chat frame was not rendered.");
    }

    private static async Task<GenerationChatBatch> SubmitParallelAsync(GenerationChatViewModel chat,
        NanoBanana2GenerationParameters? parameters = null, Action<NanoBanana2GenerationParameters>? capture = null)
    {
        ImageModelOption model = LocalCodexImageModel.Option;
        parameters ??= new NanoBanana2GenerationParameters(
            model, model.DisplayName, "Change the background", GenerationAspectRatios.Auto,
            model.Resolutions[0], 0d, 2, Array.Empty<AttachedImageDto>());
        GenerationChatBatch? batch = null;
        await chat.SubmitAsync(parameters, (submitted, _) =>
        {
            batch = submitted.ChatBatch;
            capture?.Invoke(submitted);

            return Task.CompletedTask;
        }, CancellationToken.None);

        return Assert.IsType<GenerationChatBatch>(batch);
    }

    private static async Task WaitForBusyChangeAsync(GenerationChatViewModel chat, Action complete)
    {
        TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStateChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(GenerationChatViewModel.IsBusy))
            {
                changed.TrySetResult();
            }
        }

        chat.PropertyChanged += OnStateChanged;

        try
        {
            complete();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            chat.PropertyChanged -= OnStateChanged;
        }
    }

    private sealed class ChatContext : IDisposable
    {
        public Mock<IGenerationPanelPresetTarget> Panel { get; } = new Mock<IGenerationPanelPresetTarget>();
        public GenerationChatViewModel Chat { get; }
        public Mock<IGenerationChatStore> Chats { get; } = new Mock<IGenerationChatStore>();
        public Mock<IChatTextSizeController> TextSizes { get; } = new Mock<IChatTextSizeController>();
        public Mock<IAttachedImagePreparationService> Preparation { get; } = new Mock<IAttachedImagePreparationService>();
        public Mock<IGenerationReferenceStore> References => _references;
        public Mock<TimeProvider> Clock { get; } = new Mock<TimeProvider>();
        public Mock<IAnimatedGalleryOperations> GalleryOperations { get; } = new Mock<IAnimatedGalleryOperations>();
        public Mock<IImageViewerService> ImageViewer { get; } = new Mock<IImageViewerService>();
        public GalleryViewModel Gallery => _gallery;

        private readonly LocalizationService _localization;
        private readonly Mock<IGenerationReferenceStore> _references = new();
        private readonly GalleryViewModel _gallery;
        private readonly GalleryItemsController _items = new(new PassthroughTrustedImageFileService(),
            GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestApiConfiguration.CreateGalleryOrderTimestampPolicy(),
            TestLocalizationTextProvider.Default);

        public ChatContext(IGalleryStateService? galleryStateService = null)
        {
            _gallery = GalleryViewModelTestFactory.CreateViewModel(galleryItemsController: _items,
                galleryStateService: galleryStateService, imageViewerService: ImageViewer.Object);
            AtomicArtDataPathProvider paths = new(DesktopTestDirectories.CreateUniqueDirectoryPath());
            _localization = new LocalizationService(paths, TestApiConfiguration.CreateTrustedFileStreamFactory(),
                NullLogger<Krivodeling.Localization.Avalonia.LocalizationService>.Instance, new WeakReferenceMessenger());
            _localization.SelectSavedOrEnglishFallback("English");
            TextSizes.SetupGet(controller => controller.CurrentTextSize).Returns(14d);
            Clock.Setup(clock => clock.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
            Clock.SetupGet(clock => clock.LocalTimeZone).Returns(TimeZoneInfo.Utc);
            Preparation.Setup(service => service.PrepareAsync(It.IsAny<AttachedImageDto>(),
                    It.IsAny<ImageModelOption>(), It.IsAny<CancellationToken>()))
                .Returns((AttachedImageDto image, ImageModelOption _, CancellationToken _) => Task.FromResult<AttachedImageDto?>(image));
            _references.Setup(store => store.SaveAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<AttachedImageDto>>(), It.IsAny<CancellationToken>()))
                .Returns((Guid _, IReadOnlyList<AttachedImageDto> images, CancellationToken _) =>
                    Task.FromResult<IReadOnlyList<GenerationReference>>(images.Select(image =>
                        new GenerationReference($"stored-{image.FileName}", image.FileName, image.ContentType)).ToArray()));
            Panel.Setup(panel => panel.CanApplyPreset(It.IsAny<GenerationPanelPreset>())).Returns(true);
            Panel.Setup(panel => panel.TryApplyPresetAsync(It.IsAny<GenerationPanelPreset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            GenerationChatMediaService media = new(
                _references.Object,
                Mock.Of<IFilePickerService>(),
                Preparation.Object,
                ImageViewer.Object,
                new DataRootAccessCoordinator(),
                paths,
                TestApiConfiguration.CreateTrustedFileStreamFactory(),
                GenerationImageFormatRegistryTestFactory.Create(),
                Options.Create(TestApiConfiguration.CreateGenerationOptions()));
            Chat = new GenerationChatViewModel(
                media, _references.Object, new NanoBanana2PanelTextFormatter(TestLocalizationTextProvider.Default),
                new RecordingTextClipboardService(), new TestViewModelErrorHandler(), new RecordingDialogService(),
                TextSizes.Object, Chats.Object,
                new GenerationChatDateFormatter(TestLocalizationTextProvider.Default, _localization, Clock.Object),
                GalleryOperations.Object);
            Chat.Connect(_gallery, Panel.Object);
        }

        public async Task<GenerationItemViewModel> RestoreSourceAsync(GenerationChatResult? result = null)
        {
            GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(
                imagePath: null, chatResult: result, modelId: LocalCodexImageModel.Id);
            await _gallery.RestoreStateAsync(new GalleryItemState[] { state }, CancellationToken.None);

            return _gallery.Items.Single();
        }

        public async Task<GenerationItemViewModel> RestoreSavedChatAsync(
            GenerationChatHistory history, GenerationChatGroup? group = null)
        {
            Chats.Setup(store => store.LoadAsync(history.Id, It.IsAny<CancellationToken>())).ReturnsAsync(history);
            GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(imagePath: null,
                chatId: history.Id, modelId: LocalCodexImageModel.Id, chatGroup: group);
            await _gallery.RestoreStateAsync(new GalleryItemState[] { state }, CancellationToken.None);

            return _gallery.Items.Single();
        }

        public async Task<GenerationItemViewModel> RestoreLongHistoryAsync(GenerationChatResult? result)
        {
            DateTime time = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
            GenerationReference? older = result?.Image is { } reference
                ? reference with { Hash = "earlier", FileName = "earlier.png" }
                : null;
            GenerationChatMessage[] messages = Enumerable.Range(0, 40)
                .Select(index => new GenerationChatMessage(index % 2 == 1, $"Earlier message {index}", time,
                    index == 1 && older is not null ? new GenerationReference[] { older } : Array.Empty<GenerationReference>()))
                .Append(new GenerationChatMessage(true, result?.Text ?? "Last reply", time,
                    result?.Image is { } image ? new GenerationReference[] { image } : Array.Empty<GenerationReference>())).ToArray();
            GenerationChatHistory history = new(messages, includesCardTurn: true);
            GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(
                imagePath: null, chatHistory: history, modelId: LocalCodexImageModel.Id);
            await _gallery.RestoreStateAsync(new GalleryItemState[] { state }, CancellationToken.None);

            return _gallery.Items.Single();
        }

        public GenerationChatResult AddResultCard(GenerationChatBatch batch, string text, int index = 0, bool textOnly = false)
        {
            GenerationChatResult result = textOnly ? new GenerationChatResult(text, null) { IsTextReply = true } : CreateResult(text);
            GenerationItemDto item = GenerationItemDtoTestFactory.Create(
                id: batch.Runs[index].Id, prompt: batch.UserMessage.Text, imagePath: null) with
            {
                ModelId = LocalCodexImageModel.Option.Id
            };
            GenerationStartSnapshot start = new(item.ModelId, item.ModelDisplayName, item.Prompt, item.AspectRatio,
                item.Resolution, 1, batch.UserMessage.Images.Count, item.CreatedAtUtc)
            {
                ChatHistory = batch.History,
                ChatGroup = batch.Group is { } group ? group with { VariantOrdinal = batch.Runs[index].Ordinal } : null,
                References = batch.UserMessage.Images
            };
            GenerationItemViewModel card = GenerationItemViewModel.CreatePlaceholder(start, batch.Runs[index].Id, index,
                item.CreatedAtUtc, GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
            _items.AddPlaceholders(new GenerationItemViewModel[] { card });
            card.UpdateFromResult(item, null, null, result);

            return result;
        }

        public GenerationItemViewModel AddSourceCard(string prompt)
        {
            GenerationItemDto item = GenerationItemDtoTestFactory.Create(
                id: Guid.NewGuid(), prompt: prompt, imagePath: null) with { ModelId = LocalCodexImageModel.Id };
            _gallery.AddGeneratedItems(new GenerationItemDto[] { item }, 0);

            return _gallery.Items.Single(card => card.Id == item.Id);
        }

        public GenerationChatResult CreateResult(string text)
        {
            GenerationReference reference = new("first", "first.png", GenerationImageContentTypes.Png);

            return new GenerationChatResult(text, reference);
        }

        public void VerifyOriginalsWereNotLoaded()
        {
            _references.Verify(store => store.LoadAsync(
                It.IsAny<GenerationReference>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        public GenerationReferencePreviewLoader CreateDelayedPreviewLoader(TaskCompletionSource decoding, Task ready,
            int width = 320, int height = 320)
        {
            byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(width, height);
            _references.Setup(store => store.ReadAsync(
                It.IsAny<GenerationReference>(),
                It.IsAny<Func<Stream, CancellationToken, Task<PreviewImageDimensions?>>>(),
                It.IsAny<CancellationToken>())).Returns(
                    async (GenerationReference _, Func<Stream, CancellationToken, Task<PreviewImageDimensions?>> read, CancellationToken ct) =>
                    {
                        using MemoryStream stream = new(bytes);

                        return await read(stream, ct);
                    });
            _references.Setup(store => store.ReadAsync(
                It.IsAny<GenerationReference>(),
                It.IsAny<Func<Stream, CancellationToken, Task<Bitmap?>>>(),
                It.IsAny<CancellationToken>())).Returns(
                    async (GenerationReference _, Func<Stream, CancellationToken, Task<Bitmap?>> read, CancellationToken ct) =>
                    {
                        decoding.TrySetResult();
                        await ready.WaitAsync(ct);
                        using MemoryStream stream = new(bytes);

                        return await read(stream, ct);
                    });

            return new GenerationReferencePreviewLoader(_references.Object, NullLogger<GenerationReferencePreviewLoader>.Instance);
        }

        public void Dispose()
        {
            Chat.Dispose();
            _localization.Dispose();
            _gallery.Dispose();
        }
    }
}
