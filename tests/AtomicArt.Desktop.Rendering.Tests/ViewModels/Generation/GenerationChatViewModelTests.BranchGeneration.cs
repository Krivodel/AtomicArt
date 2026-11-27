using Microsoft.Extensions.DependencyInjection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.Tests.Views.Shell;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Views.Shell;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenImageCommand_WithLiveOrDeletedBranch_OpensTheImageAndKeepsTheTree(bool deleted)
    {
        using ChatContext context = new();
        GenerationReference image = new("saved-image", "saved-image.png", GenerationImageContentTypes.Png);
        GenerationItemViewModel source = await context.RestoreSourceAsync(new GenerationChatResult("Image ready", image));
        source.ImagePath = "saved-image.png";
        GenerationItemViewModel card = source;
        GalleryImageViewerRequest? opened = null;
        context.ImageViewer.Setup(viewer => viewer.OpenAsync(It.IsAny<GalleryImageViewerRequest>(), It.IsAny<CancellationToken>()))
            .Callback((GalleryImageViewerRequest request, CancellationToken _) => opened = request)
            .Returns(Task.CompletedTask);

        if (deleted)
        {
            GenerationItemViewModel child = GenerationChatBranchMapViewModelTests.CreateCard(
                Guid.Parse("11111111-1111-1111-1111-111111111111"), source.Id);
            context.Chat.BranchMap.Open(new GenerationItemViewModel[] { child }, child.Id, child.Id);
            DateTime timestamp = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
            card = source.CreateHistoryPreview(source.Id,
                new GenerationChatMessage(false, "Saved request", timestamp, Array.Empty<GenerationReference>()),
                new GenerationChatMessage(true, "Image ready", timestamp, new GenerationReference[] { image }), source.ImagePath);
            context.Chat.BranchMap.Nodes.Single(node => node.Id == source.Id).RestorePreview(card);
        }
        else
        {
            context.Chat.BranchMap.Open(new GenerationItemViewModel[] { source }, source.Id, source.Id);
        }

        await context.Chat.BranchMap.OpenImageCommand.ExecuteAsync(card);

        context.Chat.BranchMap.IsOpen.Should().BeTrue();
        context.Chat.BranchMap.IsComposerOpen.Should().BeFalse();
        context.ImageViewer.Verify(viewer => viewer.OpenAsync(It.IsAny<GalleryImageViewerRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        opened.Should().NotBeNull();
        GalleryImageViewerSource openedSource = Assert.Single(Assert.IsType<GalleryImageViewerRequest>(opened).ItemsSource.GetItems()).Source;

        if (deleted)
        {
            openedSource.Should().Be(new GalleryReferenceImageViewerSource(image));
        }
        else
        {
            openedSource.Should().BeOfType<GalleryFileImageViewerSource>().Which.ImagePath.Should().Be(source.ImagePath);
        }
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    public async Task BranchMap_WithSelectedNode_ShowsOneOpaquePanelWithoutBackdropDrawing(double uiScale)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            Mock<ITrayService> tray = new();
            tray.SetupGet(service => service.IsExitRequested).Returns(true);
            using MainWindowLayoutTests.MainWindowTestContext context = new(services =>
            {
                services.AddSingleton(tray.Object);
                services.AddSingleton<IUiScaleService>(new RecordingUiScaleService(uiScale));
            }, includeAllViewTemplates: true);
            MainWindow window = context.Window;
            window.Width = 1200d;
            window.Height = 850d;
            MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
            GenerationChatViewModel chat = Assert.IsType<GenerationChatViewModel>(viewModel.Chat);
            UniversalNanoBananaPanelViewModel generation = Assert.IsType<UniversalNanoBananaPanelViewModel>(viewModel.ActiveGenerationPanel);
            generation.Prompt = "Continue the selected branch";
            window.Show();
            window.CaptureRenderedFrame();
            NanoBanana2PanelView original = window.GetVisualDescendants().OfType<NanoBanana2PanelView>().Single();
            Button initialGenerate = original.GetVisualDescendants().OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, generation.GenerateCommand));
            AssertGenerationButtonRendered(window, initialGenerate);
            Border panel = window.FindControl<Border>("GenerationPanelHost")
                ?? throw new InvalidOperationException("The generation panel is unavailable.");
            IBrush? originalBackground = panel.Background;
            AtomicArt.Desktop.Tests.TestUiFrameScheduler frames = new();
            panel.SetValue(AnimatedVisibilityBehavior.FrameSchedulerProperty, frames);
            GenerationItemViewModel[] cards = CreateBranchChain(3);
            chat.BranchMap.Open(cards, cards[1].Id, cards[1].Id);
            frames.RunFrame(TimeSpan.Zero);
            frames.RunFrame(TimeSpan.FromMilliseconds(400d));
            window.CaptureRenderedFrame();
            panel.IsVisible.Should().BeFalse();
            using SKBitmap withoutPanel = CaptureRenderedBitmap(window);

            chat.BranchMap.ShowComposer(cards[1]);
            panel.IsVisible.Should().BeTrue();
            panel.Opacity.Should().Be(0d);
            frames.RunFrame(TimeSpan.FromMilliseconds(500d));
            frames.RunFrame(TimeSpan.FromMilliseconds(580d));
            panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
            window.CaptureRenderedFrame();
            frames.RunFrame(TimeSpan.FromMilliseconds(900d));
            window.CaptureRenderedFrame();
            GenerationChatBranchMapView map = window.GetVisualDescendants().OfType<GenerationChatBranchMapView>().Single();
            Button selected = map.GetVisualDescendants().OfType<Button>()
                .Single(button => button.DataContext is GenerationChatBranchNodeViewModel { IsSelected: true });
            panel.Opacity.Should().Be(1d);
            await WaitForFramesAsync(window, () => ReferenceEquals(selected.BorderBrush, map.FindResource("MetadataReadyBrush")));

            window.GetVisualDescendants().OfType<NanoBanana2PanelView>().Single().Should().BeSameAs(original);
            Button generate = original.GetVisualDescendants().OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, generation.GenerateCommand));
            generate.IsEffectivelyVisible.Should().BeTrue();
            generate.Bounds.Height.Should().BeGreaterThan(0d);
            Point buttonBottom = generate.TranslatePoint(new Point(0d, generate.Bounds.Height), panel)
                ?? throw new InvalidOperationException("The generation button has no position.");
            buttonBottom.Y.Should().BeLessThanOrEqualTo(panel.Bounds.Height);
            panel.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.A.Should().Be(byte.MaxValue);
            panel.CacheMode.Should().BeOfType<BitmapCache>().Which.RenderAtScale.Should().BeApproximately(uiScale, 0.001d);
            panel.ClipToBounds.Should().BeFalse();
            panel.BoxShadow.Should().NotBe(default(BoxShadows));
            Point shadowProbe = panel.TranslatePoint(new Point(-6d, panel.Bounds.Height / 2d), window)
                ?? throw new InvalidOperationException("The panel shadow has no position.");
            using SKBitmap withPanel = CaptureRenderedBitmap(window);
            SKColor originalPixel = withoutPanel.GetPixel((int)shadowProbe.X, (int)shadowProbe.Y);
            SKColor shadowPixel = withPanel.GetPixel((int)shadowProbe.X, (int)shadowProbe.Y);
            (shadowPixel.Red + shadowPixel.Green + shadowPixel.Blue).Should()
                .BeLessThan(originalPixel.Red + originalPixel.Green + originalPixel.Blue);
            window.GetVisualDescendants().OfType<BlurBackdropControl>()
                .Should().NotContain(control => control.IsEffectivelyVisible && control.Intensity > 0d);
            Point panelCenter = panel.TranslatePoint(new Rect(panel.Bounds.Size).Center, window)
                ?? throw new InvalidOperationException("The panel has no position.");
            (window.InputHitTest(panelCenter) as Visual)?.GetSelfAndVisualAncestors().Should().Contain(panel);
            map.Bounds.Height.Should().BeGreaterThan(panel.Bounds.Height);
            using Bitmap frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The branch generation view was not rendered.");
            SaveRenderedFrame(frame, "chat-branch-generation.png");
            AssertGenerationButtonRendered(window, generate);
            PanZoomControl viewport = map.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationItemStatus originalStatus = cards[^1].StatusKind;
            cards[^1].StatusKind = GenerationItemStatus.Generating;

            for (int input = 0; input < 3; input++)
            {
                viewport.ZoomInCommand.Execute(null);
                await WaitForFramesAsync(window, () => !viewport.IsInteracting);
                AssertGenerationButtonRendered(window, generate);
            }

            cards[^1].StatusKind = originalStatus;
            Point blank = map.TranslatePoint(new Point(12d, 110d), window)
                ?? throw new InvalidOperationException("The map has no position.");
            window.MouseMove(blank);
            chat.BranchMap.IsComposerOpen.Should().BeTrue();
            window.MouseDown(blank, MouseButton.Left);
            window.MouseMove(blank + new Vector(30d, 40d));
            window.MouseUp(blank + new Vector(30d, 40d), MouseButton.Left);
            chat.BranchMap.IsComposerOpen.Should().BeTrue();
            window.MouseDown(blank, MouseButton.Left);
            window.MouseUp(blank, MouseButton.Left);
            chat.BranchMap.IsComposerOpen.Should().BeFalse();
            panel.IsVisible.Should().BeTrue();
            panel.IsHitTestVisible.Should().BeFalse();
            frames.RunFrame(TimeSpan.FromMilliseconds(1000d));
            frames.RunFrame(TimeSpan.FromMilliseconds(1080d));
            panel.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
            double interruptedOpacity = panel.Opacity;
            chat.BranchMap.ShowComposer(cards[1]);
            panel.Opacity.Should().Be(interruptedOpacity);
            frames.RunFrame(TimeSpan.FromMilliseconds(1200d));
            frames.RunFrame(TimeSpan.FromMilliseconds(1600d));
            panel.Opacity.Should().Be(1d);
            AssertGenerationButtonRendered(window, generate);
            chat.BranchMap.HideComposerCommand.Execute(null);
            frames.RunFrame(TimeSpan.FromMilliseconds(1700d));
            frames.RunFrame(TimeSpan.FromMilliseconds(2100d));
            panel.IsVisible.Should().BeFalse();
            generation.Prompt.Should().Be("Continue the selected branch");
            window.GetVisualDescendants().OfType<NanoBanana2PanelView>().Single().Should().BeSameAs(original);
            chat.BranchMap.CloseCommand.Execute(null);
            frames.RunFrame(TimeSpan.FromMilliseconds(2200d));
            frames.RunFrame(TimeSpan.FromMilliseconds(2600d));
            AssertGenerationButtonRendered(window, generate);
            panel.Background.Should().BeSameAs(originalBackground);
            panel.CacheMode.Should().BeNull();
            panel.ClipToBounds.Should().BeTrue();
        });
    }

    [Fact]
    public async Task BranchMap_WhenGenerationCardsArrive_AnimatesNewContainersWithoutReplayingExistingCards()
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            GenerationItemViewModel[] cards = CreateBranchChain(2);
            Mock<IGenerationPreviewSessionFactory> previews = new();
            previews.Setup(factory => factory.Create(It.IsAny<TopLevel>())).Returns(Mock.Of<IGenerationPreviewSession>());
            AtomicArt.Desktop.Tests.TestUiFrameScheduler frames = new();
            using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap();
            map.Open(new GenerationItemViewModel[] { cards[0] }, cards[0].Id, cards[0].Id);
            GenerationChatBranchMapView view = new(previews.Object, frames) { DataContext = map };
            Window window = Show(view, 900d, 640d);

            try
            {
                GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
                Control original = panel.Children.Single();
                original.Opacity.Should().Be(1d);
                cards[1].StatusKind = GenerationItemStatus.Generating;
                map.Refresh(cards);
                await WaitForFramesAsync(window, () => panel.Children.Count == 2 && panel.Children.Any(child => child.Opacity == 0d));
                Control added = panel.Children.Single(child => child.DataContext is GenerationChatBranchNodeViewModel node
                    && ReferenceEquals(node.Card, cards[1]));
                frames.RunFrame(TimeSpan.Zero);
                frames.RunFrame(TimeSpan.FromMilliseconds(140d));

                added.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                original.Opacity.Should().Be(1d);
                map.Refresh(cards);
                panel.Children.Should().Contain(original).And.Contain(added);
                frames.RunFrame(TimeSpan.FromMilliseconds(600d));
                added.Opacity.Should().Be(1d);
                window.Close();
                frames.RunFrame(TimeSpan.FromMilliseconds(1000d));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DoubleClick_WithCurrentOrAnotherChat_OpensTheRequestedHistory(bool anotherChat, bool delayed)
    {
        await DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreSourceAsync();
            await context.Chat.OpenCommand.ExecuteAsync(source);
            GenerationItemViewModel target = source;

            if (anotherChat)
            {
                GenerationChatBatch batch = await SubmitParallelAsync(context.Chat);
                GenerationChatResult result = context.AddResultCard(batch, "Target chat", textOnly: true);
                batch.Runs[0].Complete(result);
                batch.Runs[1].Complete(null);
                target = context.Gallery.Items.Single(card => card.Id == batch.Runs[0].Id);
                await context.Chat.OpenCommand.ExecuteAsync(source);
            }

            await context.Chat.OpenBranchMapCommand.ExecuteAsync(null);
            TaskCompletionSource<bool>? apply = null;

            if (delayed)
            {
                apply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                context.Panel.Setup(panel => panel.TryApplyPresetAsync(It.IsAny<GenerationPanelPreset>(), It.IsAny<CancellationToken>()))
                    .Returns(apply.Task);
            }
            bool composerShown = false;
            context.Chat.BranchMap.PropertyChanged += (_, args) =>
            {
                composerShown |= args.PropertyName == nameof(GenerationChatBranchMapViewModel.IsComposerOpen)
                    && context.Chat.BranchMap.IsComposerOpen;
            };
            GenerationChatBranchMapView view = new() { DataContext = context.Chat.BranchMap };
            Window window = Show(view, 900d, 640d);

            try
            {
                Button button = view.GetVisualDescendants().OfType<Button>()
                    .Single(control => control.DataContext is GenerationChatBranchNodeViewModel node && ReferenceEquals(node.Card, target));
                Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2d, button.Bounds.Height - 18d), window)
                    ?? throw new InvalidOperationException("The branch card has no position.");
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                if (delayed)
                {
                    context.Chat.IsLoading.Should().BeTrue();
                }
                else if (context.Chat.BranchMap.SelectCommand.ExecutionTask is { } selected)
                {
                    await selected;
                    window.CaptureRenderedFrame();
                    view.GetVisualDescendants().OfType<Button>()
                        .Single(control => control.DataContext is GenerationChatBranchNodeViewModel node && ReferenceEquals(node.Card, target))
                        .Should().BeSameAs(button, "the first click must preserve the target of the second click");
                }
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                context.Chat.BranchMap.OpenNodeCommand.ExecutionTask.Should().NotBeNull();
                apply?.SetResult(true);

                if (context.Chat.BranchMap.SelectCommand.ExecutionTask is { } selecting)
                {
                    await selecting;
                }

                if (delayed)
                {
                    composerShown.Should().BeFalse();
                }

                context.Chat.BranchMap.IsOpen.Should().BeFalse();
                context.Chat.IsExpanded.Should().BeTrue();
                context.Chat.Messages[^1].Text.Should().Be(anotherChat ? "Target chat" : string.Empty);
            }
            finally
            {
                apply?.TrySetResult(false);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task SelectCommand_InBranchMap_GeneratesFromSelectedHistoryAndPreservesTheDraft()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatBatch batch = await SubmitParallelAsync(context.Chat);
        context.AddResultCard(batch, "Selected branch");
        GenerationItemViewModel target = context.Gallery.Items.Single(card => card.Id == batch.Runs[0].Id);
        await context.Chat.OpenCommand.ExecuteAsync(source);
        Mock<IGenerationRunDispatcher> dispatcher = new();
        GenerationRunRequest? captured = null;
        dispatcher.Setup(service => service.EnqueueAsync(It.IsAny<GenerationRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GenerationRunRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);
        LocalCodexSettings settings = new();
        using UniversalNanoBananaPanelViewModel panel = UniversalNanoBananaPanelViewModelTests.CreateViewModel(
            imageModelOptionCatalog: new ImageModelOptionCatalog(settings), dispatcher: dispatcher.Object,
            localCodexSettings: settings);
        context.Chat.Connect(context.Gallery, panel);
        panel.ConnectChat(context.Chat);
        panel.Prompt = "Keep this draft\nand its line break";
        await panel.AttachImagesCommand.ExecuteAsync(new AttachedImageDto[]
        {
            GenerationImageTestData.CreateAttachedImage("draft.png")
        });
        AttachedImageViewModel attachment = panel.AttachedImages.Single();
        await context.Chat.OpenBranchMapCommand.ExecuteAsync(null);
        panel.GenerateCommand.CanExecute(null).Should().BeFalse();
        GenerationChatBranchMapViewModel map = context.Chat.BranchMap;

        await map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.Card == target));

        map.IsOpen.Should().BeTrue();
        map.IsComposerOpen.Should().BeTrue();
        map.SelectedNode?.Card.Should().BeSameAs(target);
        map.Nodes.Single(node => node.IsCurrent).Card.Should().BeSameAs(target);
        context.Chat.Messages[^1].Text.Should().Be("Selected branch");
        panel.Prompt.Should().Be("Keep this draft\nand its line break");
        panel.AttachedImages.Should().ContainSingle().Which.Should().BeSameAs(attachment);
        panel.SelectedModel?.Id.Should().Be(target.ModelId);
        panel.GenerateCommand.CanExecute(null).Should().BeTrue();

        await panel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = captured.Should().NotBeNull().And.BeOfType<GenerationRunRequest>().Subject;
        request.ChatRun.Should().NotBeNull();
        request.StartSnapshot.ChatGroup?.SourceCardId.Should().Be(target.Id);
        request.StartSnapshot.ChatHistory?.Messages[^1].Text.Should().Be("Selected branch");
        map.IsOpen.Should().BeTrue();
        map.IsComposerOpen.Should().BeTrue();
        request.ChatRun?.Complete(null);

        foreach (GenerationChatRun run in batch.Runs)
        {
            run.Complete(null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectCommand_WhenDismissedDuringLoading_DoesNotSwitchTheChatOrReopenTheComposer(bool closeMap)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        await context.Chat.OpenBranchMapCommand.ExecuteAsync(null);
        long version = context.Chat.ConversationVersion;
        TaskCompletionSource<bool> apply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Panel.Setup(panel => panel.TryApplyPresetAsync(It.IsAny<GenerationPanelPreset>(), It.IsAny<CancellationToken>()))
            .Returns(apply.Task);

        Task selecting = context.Chat.BranchMap.SelectCommand.ExecuteAsync(context.Chat.BranchMap.Nodes.Single());
        context.Chat.IsLoading.Should().BeTrue();

        if (closeMap)
        {
            context.Chat.BranchMap.CloseCommand.Execute(null);
        }
        else
        {
            context.Chat.BranchMap.HideComposerCommand.Execute(null);
        }

        apply.SetResult(true);
        await selecting;

        context.Chat.ConversationVersion.Should().Be(version);
        context.Chat.BranchMap.IsOpen.Should().Be(!closeMap);
        context.Chat.BranchMap.IsComposerOpen.Should().BeFalse();
        context.Chat.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task SelectCommand_WhenPresetIsUnavailable_KeepsThePreviousGenerationTarget()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        await context.Chat.OpenBranchMapCommand.ExecuteAsync(null);
        context.Panel.Setup(panel => panel.CanApplyPreset(It.IsAny<GenerationPanelPreset>())).Returns(false);

        await context.Chat.BranchMap.SelectCommand.ExecuteAsync(context.Chat.BranchMap.Nodes.Single());

        context.Chat.BranchMap.IsOpen.Should().BeTrue();
        context.Chat.BranchMap.IsComposerOpen.Should().BeFalse();
        context.Chat.CanSubmit.Should().BeFalse();
        context.Chat.IsLoading.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoryCommand_InBranchComposer_OpensHistoryWithoutDetachingTheChat(bool expand)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        await context.Chat.OpenBranchMapCommand.ExecuteAsync(null);
        await context.Chat.BranchMap.SelectCommand.ExecuteAsync(context.Chat.BranchMap.Nodes.Single());
        long version = context.Chat.ConversationVersion;

        if (expand)
        {
            context.Chat.ExpandCommand.Execute(null);
        }
        else
        {
            context.Chat.ToggleCommand.Execute(null);
        }

        context.Chat.HasChat.Should().BeTrue();
        context.Chat.IsExpanded.Should().BeTrue();
        context.Chat.BranchMap.IsOpen.Should().BeFalse();
        context.Chat.ConversationVersion.Should().Be(version);
        context.Chat.CanSubmit.Should().BeTrue();
    }

    private void AssertGenerationButtonRendered(Window window, Button button)
    {
        Point origin = button.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("The generation button has no position.");
        Point end = button.TranslatePoint(new Point(button.Bounds.Width, button.Bounds.Height), window)
            ?? throw new InvalidOperationException("The generation button has no lower corner.");
        using SKBitmap image = CaptureRenderedBitmap(window);
        int textPixels = 0;

        for (int y = Math.Max(0, (int)origin.Y); y < Math.Min(image.Height, end.Y); y++)
        {
            for (int x = Math.Max(0, (int)origin.X); x < Math.Min(image.Width, end.X); x++)
            {
                SKColor pixel = image.GetPixel(x, y);

                if (pixel.Red > 160 && pixel.Green > 160 && pixel.Blue > 160)
                {
                    textPixels++;
                }
            }
        }

        if (textPixels <= 20)
        {
            using MemoryStream encoded = new();
            image.Encode(encoded, SKEncodedImageFormat.Png, 100);
            encoded.Position = 0;
            using Bitmap failure = new(encoded);
            SaveRenderedFrame(failure, "chat-branch-generation-button.png");
        }

        textPixels.Should().BeGreaterThan(20,
            $"the generation button must contain rendered text; content={button.Content}, enabled={button.IsEnabled}");
    }
}
