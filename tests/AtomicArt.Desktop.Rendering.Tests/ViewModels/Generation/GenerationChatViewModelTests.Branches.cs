using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Views.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Fact]
    public async Task BranchMap_WhileDragging_LoadsNewlyVisibleImagesBeforeThePointerIsReleased()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using MemoryStream data = new(GalleryThumbnailTestImages.CreatePngBytes(64, 64));
            using Bitmap bitmap = new(data);
            GenerationItemViewModel[] cards = CreateBranchChain(5);

            foreach (GenerationItemViewModel card in cards)
            {
                card.ImagePath = $"{card.Id}.png";
                card.ThumbnailPath = $"{card.Id}-thumb.png";
            }

            StubGalleryPreviewBitmapProvider provider = new((_, _) => Task.FromResult<GalleryPreviewBitmapLease?>(
                new GalleryPreviewBitmapLease(bitmap, () => { })));
            TestUiFrameScheduler frames = new();
            GalleryPreviewSourceScheduler scheduler = new(frames, TestApiConfiguration.CreateGalleryOptionsWrapper());
            GenerationPreviewSessionFactory factory = new(_ => new GenerationPreviewSession(provider, scheduler));
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
            map.Open(cards, cards[2].Id, cards[2].Id);
            GenerationChatBranchMapView view = new(factory) { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                {
                    frames.RunFrame(TimeSpan.Zero);
                    return view.GetVisualDescendants().OfType<Image>().Count(image => ReferenceEquals(image.Source, bitmap)) == cards.Length;
                });

                PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
                GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
                GenerationPreviewControl preview = view.GetVisualDescendants().OfType<GenerationPreviewControl>()
                    .Single(control => ReferenceEquals(control.DataContext, cards[0]));
                Image image = preview.FindControl<Image>("PreviewImage")
                    ?? throw new InvalidOperationException("The branch preview image is unavailable.");

                for (int input = 0; input < 20; input++)
                {
                    viewport.ZoomInCommand.Execute(null);
                }

                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting && image.Source is null);
                Point node = panel.Children[0].TranslatePoint(
                    new Point(GenerationChatBranchPanel.NodeWidth / 2d, GenerationChatBranchPanel.NodeHeight / 2d), viewport)
                    ?? throw new InvalidOperationException("The branch card has no position.");
                Point start = viewport.TranslatePoint(new Point(8d, 8d), window)
                    ?? throw new InvalidOperationException("The viewport has no position.");
                Vector movement = viewport.Bounds.Center - node;
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + movement);

                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                {
                    frames.RunFrame(TimeSpan.Zero);
                    return ReferenceEquals(image.Source, bitmap);
                });

                viewport.IsInteracting.Should().BeTrue();
                image.Source.Should().BeSameAs(bitmap);
                GenerationItemViewModel added = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(
                    Guid.Parse("66666666-6666-6666-6666-666666666666"), cards[^1].Id);
                added.StatusKind = AtomicArt.Contracts.Generation.GenerationItemStatus.Generating;
                map.Refresh(cards.Append(added).ToArray());
                window.CaptureRenderedFrame();

                view.GetVisualDescendants().OfType<GenerationPreviewControl>()
                    .Single(control => ReferenceEquals(control.DataContext, cards[0])).Should().BeSameAs(preview);
                image.Source.Should().BeSameAs(bitmap);
                window.MouseUp(start + movement, MouseButton.Left);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BranchMap_AfterZoomAndFit_ReleasesDistantPreviewsAndRestoresTheVisibleOnes()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel[] cards = CreateBranchChain(5);
            HashSet<GenerationPreviewControl> attached = [];
            Mock<IGenerationPreviewSession> session = new();
            session.Setup(value => value.Attach(It.IsAny<GenerationPreviewControl>()))
                .Callback((GenerationPreviewControl preview) => attached.Add(preview));
            session.Setup(value => value.Detach(It.IsAny<GenerationPreviewControl>()))
                .Callback((GenerationPreviewControl preview) => attached.Remove(preview));
            Mock<IGenerationPreviewSessionFactory> factory = new();
            factory.Setup(value => value.Create(It.IsAny<TopLevel>())).Returns(session.Object);
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
            map.Open(cards, cards[2].Id, cards[2].Id);
            GenerationChatBranchMapView view = new(factory.Object) { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => attached.Count == cards.Length);

                for (int step = 0; step < 20; step++)
                {
                    viewport.ZoomInCommand.Execute(null);
                }

                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting && attached.Count < cards.Length);
                attached.Count.Should().BeGreaterThan(0);
                viewport.FitCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting && attached.Count == cards.Length);

                session.Verify(value => value.Detach(It.IsAny<GenerationPreviewControl>()), Times.AtLeastOnce);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.375d)]
    public async Task BranchMap_WithImages_LoadsClippedPreviewsAndVisibleConnectionsInOneSession(double scale)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using MemoryStream image = new(GalleryThumbnailTestImages.CreatePngBytes(64, 64));
            using Bitmap bitmap = new(image);
            int acquisitions = 0;
            int releases = 0;
            int sessions = 0;
            StubGalleryPreviewBitmapProvider provider = new((_, _) =>
            {
                acquisitions++;
                return Task.FromResult<GalleryPreviewBitmapLease?>(new GalleryPreviewBitmapLease(bitmap, () => releases++));
            });
            TestUiFrameScheduler frames = new();
            GalleryPreviewSourceScheduler scheduler = new(frames, TestApiConfiguration.CreateGalleryOptionsWrapper());
            GenerationPreviewSessionFactory factory = new(_ =>
            {
                sessions++;
                return new GenerationPreviewSession(provider, scheduler);
            });
            Guid root = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid child = Guid.Parse("22222222-2222-2222-2222-222222222222");
            GenerationItemViewModel[] cards =
            [
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(root),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(child, root)
            ];

            foreach (GenerationItemViewModel card in cards)
            {
                card.ImagePath = $"{card.Id}.png";
                card.ThumbnailPath = $"{card.Id}-thumb.png";
            }

            CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?> open = new(_ => Task.CompletedTask);
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(open);
            map.Open(cards, child, child);
            GenerationChatBranchMapView view = new(factory) { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                {
                    frames.RunFrame(TimeSpan.Zero);
                    return view.GetVisualDescendants().OfType<Image>().Count(control => ReferenceEquals(control.Source, bitmap)) == 2;
                });

                PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
                Control content = viewport.Child ?? throw new InvalidOperationException("The branch map has no content.");
                content.RenderTransform = new Avalonia.Media.MatrixTransform(new Matrix(scale, 0d, 0d, scale, 20d, 20d));

                sessions.Should().Be(1);
                acquisitions.Should().Be(2);
                view.GetVisualDescendants().OfType<Image>().Count(control => ReferenceEquals(control.Source, bitmap)).Should().Be(2);
                using Bitmap frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The branch previews were not captured.");
                GenerationChatViewModelTests.SaveRenderedFrame(frame, "chat-branch-map-images.png");
                GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
                Control node = panel.Children[0];
                Point corner = node.TranslatePoint(new Point(0.5d, 0.5d), window)
                    ?? throw new InvalidOperationException("The branch card has no corner position.");
                using SKBitmap pixels = CaptureRenderedBitmap(window);
                SKColor cornerColor = pixels.GetPixel((int)corner.X, (int)corner.Y);
                cornerColor.Blue.Should().BeLessThan(160, "the image must stay inside the rounded card corner");
                Point rightCorner = node.TranslatePoint(new Point(node.Bounds.Width - 0.5d, 0.5d), window)
                    ?? throw new InvalidOperationException("The branch card has no right corner position.");
                pixels.GetPixel((int)rightCorner.X, (int)rightCorner.Y).Blue.Should().BeLessThan(160);
                Point imageInterior = node.TranslatePoint(new Point(12d, 4d), window)
                    ?? throw new InvalidOperationException("The branch image has no interior position.");
                pixels.GetPixel((int)imageInterior.X, (int)imageInterior.Y).Blue.Should().BeGreaterThan(200);
                Point connection = panel.TranslatePoint(new Point((panel.Children[0].Bounds.Right + panel.Children[1].Bounds.Left) / 2d,
                    panel.Children[0].Bounds.Center.Y), window)
                    ?? throw new InvalidOperationException("The branch connection has no position.");
                Enumerable.Range(-1, 3).Max(offset => pixels.GetPixel((int)connection.X, (int)connection.Y + offset).Blue)
                    .Should().BeGreaterThan(150, "the route must stay visible between the cards");
            }
            finally
            {
                window.Close();
            }

            releases.Should().Be(acquisitions);
        });
    }

    [Fact]
    public async Task BranchMap_WithLongPrompt_ShowsEllipsisAndOpensOnDoubleClick()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), () =>
        {
            using ChatContext context = new();
            Guid root = Guid.Parse("11111111-1111-1111-1111-111111111111");
            GenerationItemViewModel card = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(root);
            card.Prompt = string.Join("\n", Enumerable.Repeat("A long prompt that must not disappear without an ellipsis.", 8));
            GenerationItemViewModel? opened = null;
            CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?> open = new(value =>
            {
                opened = value;
                return Task.CompletedTask;
            });
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(open);
            map.Open(new GenerationItemViewModel[] { card }, card.Id, card.Id);
            GenerationChatBranchMapView view = new() { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                window.CaptureRenderedFrame();
                Button button = view.GetVisualDescendants().OfType<Button>()
                    .Single(control => control.Classes.Contains("generation-chat-branch-node"));
                OverflowEllipsisTextBlock text = button.GetVisualDescendants().OfType<OverflowEllipsisTextBlock>().Single();
                text.TextLayout.TextLines.Count.Should().BeLessThanOrEqualTo(2);
                GenerationCardControlTests.GetRenderedText(text).TrimEnd().Should().EndWith("…");
                Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The branch button has no position.");

                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);

                opened.Should().BeSameAs(card);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BranchMap_OnHover_HighlightsTheHoveredRouteWithoutRebuildingGeometry()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            Guid root = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid first = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Guid second = Guid.Parse("33333333-3333-3333-3333-333333333333");
            GenerationItemViewModel[] cards =
            [
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(root),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(first, root),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(second, root)
            ];
            CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?> open = new(_ => Task.CompletedTask);
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(open);
            map.Open(cards, first, first);
            GenerationChatBranchMapView view = new() { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                window.CaptureRenderedFrame();
                GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
                Avalonia.Media.DrawingGroup geometry = panel.ConnectionDrawing;
                panel.HighlightedNodeIds.Should().BeEquivalentTo(new Guid[] { root, first });
                Control target = panel.Children.Single(control => control.DataContext is GenerationChatBranchNodeViewModel node && node.Id == second);
                target.InvalidateMeasure();
                window.CaptureRenderedFrame();
                panel.ConnectionDrawing.Should().BeSameAs(geometry);
                Point point = target.TranslatePoint(new Point(target.Bounds.Width / 2d, target.Bounds.Height / 2d), window)
                    ?? throw new InvalidOperationException("The hovered card has no position.");
                Avalonia.Media.Color neutral = panel.ConnectionBrush.Should().BeAssignableTo<Avalonia.Media.ISolidColorBrush>().Subject.Color;
                Avalonia.Media.Color active = panel.CurrentConnectionBrush.Should().BeAssignableTo<Avalonia.Media.ISolidColorBrush>().Subject.Color;
                GenerationChatBranchConnectionControl line = view.GetVisualDescendants().OfType<GenerationChatBranchConnectionControl>()
                    .Single(control => control.Stroke is Avalonia.Media.ISolidColorBrush brush && brush.Color == neutral);
                Avalonia.Animation.BrushTransition transition = line.Transitions.Should().ContainSingle().Which
                    .Should().BeOfType<Avalonia.Animation.BrushTransition>().Subject;
                transition.Duration.Should().Be(TimeSpan.FromMilliseconds(150d));
                line.Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.BrushTransition
                    {
                        Property = GenerationChatBranchConnectionControl.StrokeProperty,
                        Duration = TimeSpan.FromSeconds(1d)
                    }
                };

                window.MouseMove(point);

                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                    line.Stroke is Avalonia.Media.ISolidColorBrush brush && brush.Color != neutral && brush.Color != active);
                panel.HighlightedNodeIds.Should().BeEquivalentTo(new Guid[] { root, second });
                panel.ConnectionDrawing.Should().BeSameAs(geometry);
                PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
                Point outside = viewport.TranslatePoint(new Point(2d, 2d), window)
                    ?? throw new InvalidOperationException("The map viewport has no position.");
                window.MouseMove(outside);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () =>
                    line.Stroke is Avalonia.Media.ISolidColorBrush brush && brush.Color == neutral);
                panel.HighlightedNodeIds.Should().BeEquivalentTo(new Guid[] { root, first });
                panel.ConnectionDrawing.Should().BeSameAs(geometry);

                map.UpdateCurrentChat(cards[2]);
                window.CaptureRenderedFrame();

                panel.HighlightedNodeIds.Should().BeEquivalentTo(new Guid[] { root, second });
                panel.ConnectionDrawing.Should().BeSameAs(geometry);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ReturnToSourceButton_OverMessages_KeepsItsLabelVisibleAndBackgroundOpaque()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            DateTime timestamp = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);
            GenerationChatMessage[] messages = Enumerable.Range(0, 20).SelectMany(index => new GenerationChatMessage[]
            {
                new(false, $"User request {index}", timestamp, Array.Empty<GenerationReference>()),
                new(true, $"Assistant response {index}", timestamp, Array.Empty<GenerationReference>())
            }).ToArray();
            await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSavedChatAsync(
                new GenerationChatHistory(messages, includesCardTurn: true)));
            GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 640d, 500d);

            try
            {
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                Button button = view.GetVisualDescendants().OfType<Button>()
                    .Single(control => ReferenceEquals(control.Command, context.Chat.ReturnToSourceCommand));
                button.Background.Should().BeSameAs(view.FindResource("MetadataRepeatBrush"));
                button.Foreground.Should().BeSameAs(view.FindResource("MetadataRepeatTextBrush"));
                TextBlock label = button.GetVisualDescendants().OfType<TextBlock>().Single();
                label.TextLayout.Height.Should().BeLessThanOrEqualTo(label.Bounds.Height + 0.01d);
                Point bottom = label.TranslatePoint(new Point(0d, label.TextLayout.Height), button)
                    ?? throw new InvalidOperationException("The return button label has no position.");
                bottom.Y.Should().BeLessThanOrEqualTo(button.Bounds.Height - button.Padding.Bottom);
                ScrollViewer scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
                Border bubble = view.GetVisualDescendants().OfType<Border>()
                    .First(border => border.Classes.Contains("generation-chat-bubble")
                        && border.DataContext is GenerationChatMessageViewModel { IsAssistant: true });
                Point sample = new(button.Padding.Left / 2d, button.Bounds.Height / 2d);
                Point sampleInScroll = button.TranslatePoint(sample, scroll)
                    ?? throw new InvalidOperationException("The button has no scroll position.");
                Point bubbleOrigin = bubble.TranslatePoint(default, scroll)
                    ?? throw new InvalidOperationException("The message has no scroll position.");
                scroll.Offset = new Vector(0d, scroll.Offset.Y + bubbleOrigin.Y - sampleInScroll.Y + (bubble.Bounds.Height / 2d));
                window.CaptureRenderedFrame()?.Dispose();
                Point inBubble = button.TranslatePoint(sample, bubble)
                    ?? throw new InvalidOperationException("The button and message have no shared position.");
                new Rect(bubble.Bounds.Size).Contains(inBubble).Should().BeTrue();
                Point pointer = button.TranslatePoint(sample, window)
                    ?? throw new InvalidOperationException("The button has no window position.");
                const int HoverSettlingMilliseconds = 450;

                foreach (bool hovered in new bool[] { false, true })
                {
                    window.MouseMove(hovered ? pointer : new Point(window.Bounds.Width / 2d, window.Bounds.Height - 1d));
                    await Task.Delay(HoverSettlingMilliseconds);
                    button.IsPointerOver.Should().Be(hovered);
                    button.Background.Should().BeSameAs(view.FindResource(hovered ? "MetadataRepeatHoverBrush" : "MetadataRepeatBrush"));
                    Point pixel = button.TranslatePoint(sample, window)
                        ?? throw new InvalidOperationException("The button sample has no window position.");
                    bubble.Background = Brushes.LimeGreen;
                    using SKBitmap green = CaptureRenderedBitmap(window);
                    bubble.Background = Brushes.Blue;
                    using SKBitmap blue = CaptureRenderedBitmap(window);

                    blue.GetPixel((int)pixel.X, (int)pixel.Y).Should().Be(green.GetPixel((int)pixel.X, (int)pixel.Y),
                        "message colors must not show through the return button");
                }

                GenerationChatViewModelTests.SaveChatFrame(window, "return-to-source-opaque.png");
                window.MouseDown(pointer, MouseButton.Left);
                window.MouseUp(pointer, MouseButton.Left);
                await (context.Chat.ReturnToSourceCommand.ExecutionTask
                    ?? throw new InvalidOperationException("The return command did not start."));
                context.Chat.CanReturnToSource.Should().BeFalse();
            }
            finally
            {
                window.Close();

                foreach (GenerationChatRun run in batch.Runs)
                {
                    run.Complete(null);
                }
            }
        });
    }

    [Fact]
    public async Task OpenNodeCommand_WithGeneratingSource_OpensItsPendingReply()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        source.StatusKind = AtomicArt.Contracts.Generation.GenerationItemStatus.Generating;
        await context.Chat.OpenCommand.ExecuteAsync(source);
        context.Chat.OpenBranchMapCommand.Execute(null);

        await context.Chat.BranchMap.OpenNodeCommand.ExecuteAsync(context.Chat.BranchMap.SelectedNode);

        context.Chat.BranchMap.IsOpen.Should().BeFalse();
        context.Chat.Messages[^1].Images.Should().ContainSingle(image => image.IsPending);
        context.Chat.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task OpenBranchMapCommand_ThenEscape_PreservesTheChatAndItsEditor()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            GenerationItemViewModel source = await context.RestoreSourceAsync();
            await context.Chat.OpenCommand.ExecuteAsync(source);
            GenerationChatMessageViewModel prompt = context.Chat.Messages[0];
            prompt.EditCommand.Execute(null);
            prompt.EditText = "Uncommitted changes";
            GenerationChatMessageViewModel[] messages = context.Chat.Messages.ToArray();
            context.Chat.OpenBranchMapCommand.Execute(null);
            GenerationChatBranchMapView view = new() { DataContext = context.Chat.BranchMap };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                window.CaptureRenderedFrame();
                view.IsKeyboardFocusWithin.Should().BeTrue();
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

                context.Chat.BranchMap.IsOpen.Should().BeFalse();
                context.Chat.IsExpanded.Should().BeTrue();
                context.Chat.Messages.Should().Equal(messages);
                prompt.EditText.Should().Be("Uncommitted changes");
                prompt.IsEditing.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BranchMap_WithMultipleLevels_FocusesTheCurrentChatAndKeepsFitAndPanWorking()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            Guid rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid firstId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Guid secondId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            Guid leafId = Guid.Parse("44444444-4444-4444-4444-444444444444");
            GenerationItemViewModel[] cards =
            [
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(rootId),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(firstId, rootId),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(secondId, rootId),
                AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateCard(leafId, firstId)
            ];
            CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?> open = new(_ => Task.CompletedTask);
            using GenerationChatBranchMapViewModel map = AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests.CreateMap(open);
            map.Open(cards, leafId, leafId);
            GenerationChatBranchMapView view = new() { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 900d, 640d);

            try
            {
                window.CaptureRenderedFrame();
                PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
                GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
                Rect[] positions = panel.Children.Select(child => child.Bounds).ToArray();
                viewport.Bounds.Size.Should().Be(view.Bounds.Size);
                Control current = panel.Children.Single(child => child.DataContext is GenerationChatBranchNodeViewModel { IsCurrent: true });
                Point center = current.TranslatePoint(new Rect(current.Bounds.Size).Center, viewport)
                    ?? throw new InvalidOperationException("The current chat has no position.");
                center.X.Should().BeApproximately(viewport.Bounds.Width / 2d, 0.01d);
                center.Y.Should().BeApproximately(viewport.Bounds.Height / 2d, 0.01d);
                viewport.Zoom.Should().Be(1d);
                viewport.IsInteracting.Should().BeFalse();
                viewport.FitCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting);

                foreach (Control node in panel.Children)
                {
                    Point top = node.TranslatePoint(default, viewport) ?? throw new InvalidOperationException("The branch node has no position.");
                    Point bottom = node.TranslatePoint(new Point(node.Bounds.Width, node.Bounds.Height), viewport)
                        ?? throw new InvalidOperationException("The branch node has no lower edge.");
                    top.X.Should().BeGreaterThanOrEqualTo(0d);
                    top.Y.Should().BeGreaterThanOrEqualTo(0d);
                    bottom.X.Should().BeLessThanOrEqualTo(viewport.Bounds.Width);
                    bottom.Y.Should().BeLessThanOrEqualTo(viewport.Bounds.Height);
                }

                viewport.ZoomInCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting);
                panel.Children.Select(child => child.Bounds).Should().Equal(positions);
                Point beforeDrag = panel.TranslatePoint(default, viewport)
                    ?? throw new InvalidOperationException("The branch map has no position.");
                Point start = viewport.TranslatePoint(new Point(5d, 5d), window)
                    ?? throw new InvalidOperationException("The map viewport has no position.");
                Vector distance = new(70d, 40d);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + distance);
                window.MouseUp(start + distance, MouseButton.Left);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting);
                Point afterDrag = panel.TranslatePoint(default, viewport)
                    ?? throw new InvalidOperationException("The moved map has no position.");
                (afterDrag.X - beforeDrag.X).Should().BeApproximately(distance.X, 0.01d);
                (afterDrag.Y - beforeDrag.Y).Should().BeApproximately(distance.Y, 0.01d);
                panel.Children.Select(child => child.Bounds).Should().Equal(positions);
                viewport.FitCommand.Execute(null);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => !viewport.IsInteracting);
                using Bitmap frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The branch map was not captured.");
                GenerationChatViewModelTests.SaveRenderedFrame(frame, "chat-branch-map.png");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static GenerationItemViewModel[] CreateBranchChain(int count)
    {
        Guid[] ids = Enumerable.Range(1, count).Select(index => new Guid(index, 0, 0, new byte[8])).ToArray();

        return ids.Select((id, index) => AtomicArt.Desktop.Tests.ViewModels.Generation.GenerationChatBranchMapViewModelTests
            .CreateCard(id, index == 0 ? null : ids[index - 1])).ToArray();
    }

    private static void SaveRenderedFrame(Bitmap frame, string name)
    {
        string artifacts = Path.Combine(AppContext.BaseDirectory, "ChatVisualChecks");
        Directory.CreateDirectory(artifacts);
        frame.Save(Path.Combine(artifacts, name));
    }
}
