using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(1024, 512, 256, 128)]
    [InlineData(512, 1024, 128, 256)]
    public async Task AttachmentPreview_WithRealDecoder_ReducesImageAndPreservesOriginalBytes(
        int sourceWidth, int sourceHeight, int previewWidth, int previewHeight)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), () =>
        {
            byte[] content = GalleryThumbnailTestImages.CreatePngBytes(sourceWidth, sourceHeight);
            AttachedImageViewModel item = new(new AttachedImageDto("large.png", "image/png", content));
            AnimatedAttachmentListControl control = new() { Items = new AttachedImageViewModel[] { item } };
            Window window = DesktopControlTestBase.Show(control, 160d, 96d);

            try
            {
                Image image = control.GetVisualDescendants().OfType<Image>().Single();
                Bitmap preview = Assert.IsType<Bitmap>(image.Source);

                preview.PixelSize.Should().Be(new PixelSize(previewWidth, previewHeight));
                item.ToDto().Content.Should().Equal(content);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithReferenceEditor_AnimatesActionsAndKeepsRemovedPreviewUntilFadeEnds()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(new TaskCompletionSource(), Task.CompletedTask);
            GenerationReference first = new("first", "first.png", "image/png");
            GenerationReference second = new("second", "second.png", "image/png");
            GenerationChatMessage saved = new(false, "A message with references", new DateTime(2026, 11, 24), new[] { first, second });
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(new[] { saved }, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, 560d, 480d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                window.CaptureRenderedFrame()?.Dispose();
                GenerationChatMessageView row = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Single();
                GenerationChatMessageViewModel model = context.Chat.Messages.Single();
                AnimatedAttachmentListControl list = row.GetVisualDescendants().OfType<AnimatedAttachmentListControl>().Single();
                GenerationReferencePreviewControl preview = list.GetVisualDescendants().OfType<GenerationReferencePreviewControl>().First();
                GenerationAttachmentStripView strip = row.GetVisualDescendants().OfType<GenerationAttachmentStripView>().Single();
                Point origin = preview.TranslatePoint(default, strip) ?? throw new InvalidOperationException();

                model.EditCommand.Execute(null);
                window.UpdateLayout();
                (preview.TranslatePoint(default, strip) ?? throw new InvalidOperationException()).X.Should().BeApproximately(origin.X, 1d);
                List<double> positions = new List<double>();

                for (int frame = 0; frame < 24; frame++)
                {
                    await Task.Delay(16);
                    window.CaptureRenderedFrame()?.Dispose();
                    positions.Add((preview.TranslatePoint(default, strip) ?? throw new InvalidOperationException()).X);
                }

                double end = positions[^1];
                positions.Should().Contain(position => position > Math.Min(origin.X, end) + 1d && position < Math.Max(origin.X, end) - 1d);
                object bitmap = preview.Source ?? throw new InvalidOperationException();
                model.RemoveReferenceCommand.Execute(model.AttachedImages[0]);
                window.CaptureRenderedFrame()?.Dispose();
                preview.Source.Should().BeSameAs(bitmap);
                preview.IsAttachedToVisualTree().Should().BeTrue();
                await Task.Delay(80);
                window.CaptureRenderedFrame()?.Dispose();
                Control retiring = preview.GetVisualAncestors().OfType<Border>().First();
                retiring.Opacity.Should().BeInRange(0d, 1d);
                model.CancelCommand.Execute(null);
                window.UpdateLayout();
                model.Images.Select(image => image.Reference).Should().Equal(first, second);
                row.EditorProgress.Should().BeGreaterThan(0d);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => row.EditorProgress == 0d);
                await Task.Delay(500);
                window.CaptureRenderedFrame()?.Dispose();
                list.GetVisualDescendants().OfType<GenerationReferencePreviewControl>().Should().HaveCount(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ChatView_WithAssistantImageRemovalAndCancel_FadesBetweenIndependentImageSets()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            using GenerationReferencePreviewLoader loader = context.CreateDelayedPreviewLoader(new TaskCompletionSource(), Task.CompletedTask);
            GenerationReference first = new("first", "first.png", "image/png");
            GenerationReference second = new("second", "second.png", "image/png");
            GenerationChatMessage saved = new(true, "Two images", new DateTime(2026, 11, 24), new[] { first, second });
            GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(new[] { saved }, includesCardTurn: true));
            GenerationChatView view = new() { DataContext = context.Chat, PreviewLoader = loader };
            Window window = DesktopControlTestBase.Show(view, 560d, 700d);

            try
            {
                await context.Chat.OpenCommand.ExecuteAsync(source);
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                await Task.Delay(450);
                GenerationChatMessageViewModel model = context.Chat.Messages.Single();
                model.EditCommand.Execute(null);
                GenerationChatMessageView row = view.GetVisualDescendants().OfType<GenerationChatMessageView>().Single();
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => row.EditorProgress == 1d);
                GenerationChatImageSet old = model.ImageSet;
                TransitioningContentControl transition = row.GetVisualDescendants().OfType<TransitioningContentControl>().Single();
                TestUiFrameScheduler frames = new();
                transition.PageTransition = new UiCrossFadeTransition(_ => frames)
                {
                    Duration = TimeSpan.FromMilliseconds(280)
                };

                model.RemoveResultCommand.Execute(model.Images[0]);
                window.UpdateLayout();

                old.IsCurrent.Should().BeFalse();
                old.Images.Should().HaveCount(2);
                model.ImageSet.Images.Should().ContainSingle();
                ItemsControl[] sets = row.GetVisualDescendants().OfType<ItemsControl>()
                    .Where(control => control.Classes.Contains("generation-chat-images")).ToArray();
                sets.Length.Should().Be(2);
                sets.Single(control => object.ReferenceEquals(control.DataContext, old)).IsHitTestVisible.Should().BeFalse();
                frames.RunFrame(TimeSpan.Zero);
                frames.RunFrame(TimeSpan.FromMilliseconds(140));
                window.CaptureRenderedFrame()?.Dispose();
                sets.Any(control => control.GetVisualAncestors().Any(parent => parent.Opacity is > 0d and < 1d)).Should().BeTrue();

                model.CancelCommand.Execute(null);
                window.UpdateLayout();
                await Task.Yield();
                model.ImageSet.Images.Select(image => image.Reference).Should().Equal(first, second);
                frames.RunFrame(TimeSpan.FromMilliseconds(200));
                frames.RunFrame(TimeSpan.FromMilliseconds(340));
                frames.RunFrame(TimeSpan.FromMilliseconds(500));
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => row.GetVisualDescendants().OfType<ItemsControl>()
                    .Count(control => control.Classes.Contains("generation-chat-images")) == 1);
                await Task.Delay(350);
                window.CaptureRenderedFrame()?.Dispose();
                GenerationChatViewModelTests.SaveChatFrame(window, "restored-assistant-images.png");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
