using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Lang.Avalonia;
using Moq;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Fact]
    public async Task OpenBranchMapCommand_WithSlowHistory_KeepsNavigationAvailableAndCancelsWhenClosed()
    {
        using ChatContext context = new();
        Guid rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatHistory history = new(new GenerationChatMessage[]
        {
            new(false, "Root", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Root reply", timestamp, Array.Empty<GenerationReference>()),
            new(false, "Current", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Current reply", timestamp, Array.Empty<GenerationReference>())
        }, includesCardTurn: true);
        GenerationChatGroup group = new(Guid.Parse("22222222-2222-2222-2222-222222222222"),
            rootId, history.Id, Array.Empty<string>());
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(history, group);
        await context.Chat.OpenCommand.ExecuteAsync(source);
        source.ChatHistory = null;
        TaskCompletionSource<GenerationChatHistory> reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Chats.Setup(store => store.LoadAsync(history.Id, It.IsAny<CancellationToken>())).Returns(reading.Task);

        Task opening = context.Chat.OpenBranchMapCommand.ExecuteAsync(null);

        context.Chat.BranchMap.IsOpen.Should().BeTrue();
        context.Chat.IsBusy.Should().BeFalse();
        context.Chat.OpenCommand.CanExecute(source).Should().BeTrue();
        context.Chat.Collapse();
        reading.SetResult(history);
        await opening;
        context.Chat.BranchMap.IsOpen.Should().BeFalse();
        context.Chat.BranchMap.Nodes.Single(node => node.Id == rootId).HasPreview.Should().BeFalse();
    }

    [Fact]
    public async Task BranchMap_WithDeletedAncestor_ShowsItsHistoryAndFlashesTheWholeCardOnDoubleClick()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            Guid rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid currentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            GenerationItemViewModel current = GenerationChatBranchMapViewModelTests.CreateCard(currentId, rootId);
            current.ImagePath = "current.png";
            current.ThumbnailPath = "current-thumb.png";
            GenerationReference reference = new("root", "root.png", GenerationImageContentTypes.Png);
            DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
            current.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
            {
                new(false, "Original lake at sunset", timestamp, Array.Empty<GenerationReference>()),
                new(true, "Original reply", timestamp, new GenerationReference[] { reference })
            });
            Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
            references.Setup(store => store.GetExistingFilePathAsync(reference, It.IsAny<CancellationToken>()))
                .ReturnsAsync("History/root.png");
            int opened = 0;
            using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ =>
            {
                opened++;
                return Task.CompletedTask;
            }));
            map.Open(new GenerationItemViewModel[] { current }, currentId, currentId);
            await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), references.Object, CancellationToken.None);
            using MemoryStream data = new(GalleryThumbnailTestImages.CreatePngBytes(64, 64));
            using Bitmap bitmap = new(data);
            List<string> acquired = [];
            StubGalleryPreviewBitmapProvider provider = new((path, _) =>
            {
                acquired.Add(path);
                return Task.FromResult<GalleryPreviewBitmapLease?>(new GalleryPreviewBitmapLease(bitmap, () => { }));
            });
            TestUiFrameScheduler frames = new();
            GalleryPreviewSourceScheduler scheduler = new(frames, TestApiConfiguration.CreateGalleryOptionsWrapper());
            GenerationPreviewSessionFactory factory = new(_ => new GenerationPreviewSession(provider, scheduler));
            GenerationChatBranchMapView view = new(factory, frames) { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 1100d, 850d);

            try
            {
                await WaitForFramesAsync(window, () =>
                {
                    frames.RunFrame(TimeSpan.Zero);
                    return view.GetVisualDescendants().OfType<Image>().Count(image => ReferenceEquals(image.Source, bitmap)) == 2;
                });
                Button deleted = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => button.DataContext is GenerationChatBranchNodeViewModel { IsDeleted: true });
                deleted.GetVisualDescendants().OfType<OverflowEllipsisTextBlock>().Single().Text
                    .Should().Be("Original lake at sunset");
                string deletedLabel = I18nManager.Instance.GetResource(GenerationChatLocalizationKeys.DeletedChat);
                deleted.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible)
                    .Select(text => text.Text).Should().Contain(deletedLabel);
                deleted.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == deletedLabel)
                    .Foreground.Should().BeSameAs(view.FindResource("MetadataFailedBrush"));
                acquired.Should().Contain("History/root.png");
                Border feedback = deleted.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("generation-chat-branch-feedback"));
                Point origin = deleted.TranslatePoint(default, window) ?? throw new InvalidOperationException();
                Rect region = new(origin, deleted.Bounds.Size);
                Point point = region.Center;
                window.MouseMove(point);
                using SKBitmap baseline = CaptureRenderedBitmap(window);
                using Bitmap rendered = window.CaptureRenderedFrame() ?? throw new InvalidOperationException();
                SaveRenderedFrame(rendered, "deleted-chat-branch.png");
                (window.InputHitTest(point) as Visual)?.GetSelfAndVisualAncestors().Should().Contain(deleted);

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    for (int click = 0; click < 4; click++)
                    {
                        window.MouseDown(point, MouseButton.Left);
                        window.MouseUp(point, MouseButton.Left);
                    }

                    deleted.Classes.Should().Contain("feedback", $"attempt {attempt} must start the deleted-chat feedback");
                    TimeSpan start = TimeSpan.FromSeconds(attempt + 1);
                    frames.RunFrame(start);
                    frames.RunFrame(start + TimeSpan.FromMilliseconds(150d));
                    feedback.Opacity.Should().Be(1d);
                    deleted.CacheMode.Should().BeNull();
                    using SKBitmap red = CaptureRenderedBitmap(window);
                    const double MinimumFillCoverage = 0.9d;
                    CountRedFeedbackPixels(baseline, red, region).Should()
                        .BeGreaterThan((int)(region.Width * region.Height * MinimumFillCoverage));
                    frames.RunFrame(start + TimeSpan.FromMilliseconds(300d));
                    feedback.Opacity.Should().Be(0d);
                    frames.RunFrame(start + TimeSpan.FromMilliseconds(450d));
                    feedback.Opacity.Should().Be(1d);
                    frames.RunFrame(start + TimeSpan.FromMilliseconds(600d));
                    deleted.Classes.Should().NotContain("feedback");
                    feedback.Opacity.Should().Be(0d);
                    deleted.CacheMode.Should().BeSameAs(view.CardCache);
                    using SKBitmap restored = CaptureRenderedBitmap(window);
                    CountRedFeedbackPixels(baseline, restored, region).Should().Be(0);
                }

                opened.Should().Be(0);
                map.IsOpen.Should().BeTrue();
                map.OpenNodeCommand.CanExecute(map.SelectedNode).Should().BeFalse();
                map.SelectedNode?.Id.Should().Be(rootId);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
