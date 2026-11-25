using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Rendering.Tests.Controls;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Views.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, 0)]
    [InlineData(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, 0)]
    [InlineData(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, 0)]
    [InlineData(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, 2)]
    public async Task BranchPreview_WithGalleryModifier_ExpandsWithoutClippingAndRestoresItsCard(
        Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, int zoomSteps)
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            GenerationPreviewControl preview = previews[0];
            Control owner = panel.Children[0];
            Button card = owner.GetVisualDescendants().OfType<Button>().Single();
            Grid expansion = preview.FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The branch expansion host is unavailable.");
            Rect[] positions = panel.Children.Select(child => child.Bounds).ToArray();
            (Visual Visual, bool ClipToBounds, Geometry? Clip)[] clips = preview.GetVisualAncestors()
                .TakeWhile(visual => !ReferenceEquals(visual, viewport))
                .Select(visual => (visual, visual.ClipToBounds, visual.Clip)).ToArray();

            for (int step = 0; step < zoomSteps; step++)
            {
                viewport.ZoomInCommand.Execute(null);
            }

            await WaitForFramesAsync(window, () => !viewport.IsInteracting);
            window.MouseMove(GetBranchPreviewPoint(preview, window));
            window.KeyPress(key, modifiers, physicalKey, null);
            await WaitForFramesAsync(window, () => expansion.Bounds.Height >= preview.PreviewSize * 1.6d);

            owner.Classes.Should().Contain(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
            card.CacheMode.Should().BeNull();
            panel.Children.Select(child => child.Bounds).Should().Equal(positions);
            Point imageBottom = expansion.TranslatePoint(new Point(expansion.Bounds.Width / 2d, expansion.Bounds.Height - 8d), window)
                ?? throw new InvalidOperationException("The enlarged image has no position.");
            using SKBitmap rendered = CaptureRenderedBitmap(window);
            rendered.GetPixel((int)imageBottom.X, (int)imageBottom.Y).Blue.Should().BeGreaterThan(200);

            window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
            await WaitForFramesAsync(window, () => !owner.Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass));

            card.CacheMode.Should().BeSameAs(view.CardCache);
            panel.Children.Select(child => child.Bounds).Should().Equal(positions);

            foreach ((Visual visual, bool clipToBounds, Geometry? clip) in clips)
            {
                visual.ClipToBounds.Should().Be(clipToBounds);
                visual.Clip.Should().BeSameAs(clip);
            }
        });
    }

    [Fact]
    public async Task BranchPreview_WhenAnotherPreviewExpandsDuringCollapse_SwitchesToTheNewImageWithoutClipping()
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            Grid first = previews[0].FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The first expansion host is unavailable.");
            Grid second = previews[1].FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The second expansion host is unavailable.");
            window.MouseMove(GetBranchPreviewPoint(previews[0], window));
            window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            await WaitForFramesAsync(window, () => first.Bounds.Height >= previews[0].PreviewSize * 1.6d);

            window.MouseMove(GetBranchPreviewPoint(previews[1], window), RawInputModifiers.Shift);
            await WaitForFramesAsync(window, () =>
                !panel.Children[0].Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass)
                && second.Bounds.Height >= previews[1].PreviewSize * 1.6d);

            panel.Children[1].Classes.Should().Contain(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
            Point imageBottom = second.TranslatePoint(new Point(second.Bounds.Width / 2d, second.Bounds.Height - 8d), window)
                ?? throw new InvalidOperationException("The second enlarged image has no position.");
            using SKBitmap rendered = CaptureRenderedBitmap(window);
            rendered.GetPixel((int)imageBottom.X, (int)imageBottom.Y).Blue.Should().BeGreaterThan(200);

            window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
            await WaitForFramesAsync(window, () => !panel.Children[1].Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass));
        });
    }

    [Fact]
    public async Task BranchCard_WhenHovered_ShowsItsBorderWithoutChangingLayout()
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            Button card = panel.Children[0].GetVisualDescendants().OfType<Button>().Single();
            Rect initial = card.Bounds;
            ((ISolidColorBrush?)card.BorderBrush)?.Color.A.Should().Be(0);

            window.MouseMove(GetBranchPreviewPoint(previews[0], window));
            await WaitForFramesAsync(window, () => card.BorderBrush is ISolidColorBrush { Color.A: > 0 });

            card.Bounds.Should().Be(initial);
            card.BorderThickness.Left.Should().BeGreaterThan(0d);
            window.MouseMove(new Point(2d, 2d));
            await WaitForFramesAsync(window, () => card.BorderBrush is ISolidColorBrush { Color.A: 0 });
            card.Bounds.Should().Be(initial);
        });
    }

    [Theory]
    [InlineData(MouseButton.Left, RawInputModifiers.LeftMouseButton)]
    [InlineData(MouseButton.Middle, RawInputModifiers.MiddleMouseButton)]
    [InlineData(MouseButton.Right, RawInputModifiers.RightMouseButton)]
    public async Task BranchPreview_WhenDragged_PansTheMapWithoutSelectingTheCard(MouseButton button, RawInputModifiers modifiers)
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            GenerationChatBranchMapViewModel map = view.DataContext as GenerationChatBranchMapViewModel
                ?? throw new InvalidOperationException("The branch map has no view model.");
            GenerationChatBranchNodeViewModel? selected = map.SelectedNode;
            Point initial = panel.TranslatePoint(default, viewport)
                ?? throw new InvalidOperationException("The branch panel has no position.");
            Point start = GetBranchPreviewPoint(previews[1], window);
            Vector movement = new(70d, 40d);

            window.MouseDown(start, button);
            window.MouseMove(start + movement, modifiers);
            window.MouseUp(start + movement, button);
            await WaitForFramesAsync(window, () => !viewport.IsInteracting);

            Point final = panel.TranslatePoint(default, viewport)
                ?? throw new InvalidOperationException("The moved branch panel has no position.");
            (final.X - initial.X).Should().BeApproximately(movement.X, 0.01d);
            (final.Y - initial.Y).Should().BeApproximately(movement.Y, 0.01d);
            map.SelectedNode.Should().BeSameAs(selected);
        });
    }

    [Fact]
    public async Task BranchPreview_WhenGenerationStartsAndFinishes_AnimatesProgressAndCachesOnlyTheStaticCard()
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            GenerationPreviewControl preview = previews[0];
            GenerationItemViewModel item = preview.DataContext as GenerationItemViewModel
                ?? throw new InvalidOperationException("The branch preview has no card.");
            Button card = preview.GetVisualAncestors().OfType<Button>().First();
            string? imagePath = item.ImagePath;
            string? thumbnailPath = item.ThumbnailPath;
            card.CacheMode.Should().BeSameAs(view.CardCache);

            item.ImagePath = null;
            item.ThumbnailPath = null;
            item.StatusKind = GenerationItemStatus.Generating;
            AttachmentPixelLoadingControl indicator = preview.GetVisualDescendants().OfType<AttachmentPixelLoadingControl>().Single();
            indicator.IsActive.Should().BeTrue();
            indicator.IsVisible.Should().BeTrue();
            using SKBitmap before = CaptureRenderedBitmap(window);
            await Task.Delay(160);
            using SKBitmap after = CaptureRenderedBitmap(window);

            card.CacheMode.Should().BeNull();
            PixelLoadingRenderingTests.CountChangedPixels(before, after).Should().BeGreaterThan(100);
            item.ImagePath = imagePath;
            item.ThumbnailPath = thumbnailPath;
            item.StatusKind = GenerationItemStatus.Generated;
            window.CaptureRenderedFrame()?.Dispose();
            card.CacheMode.Should().BeSameAs(view.CardCache);
        });
    }

    private static Point GetBranchPreviewPoint(GenerationPreviewControl preview, Window window)
    {
        double height = (double)(preview.FindResource("GenerationChatBranchPreviewHeight")
            ?? throw new InvalidOperationException("The branch preview height is unavailable."));

        return preview.TranslatePoint(new Point(preview.PreviewSize / 2d, height / 2d), window)
            ?? throw new InvalidOperationException("The branch preview has no position.");
    }

    private static async Task VerifyBranchPreviewsAsync(
        Func<Window, GenerationChatBranchMapView, GenerationPreviewControl[], Task> verify, Size? sourceSize = null)
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            Size imageSize = sourceSize ?? new Size(64d, 64d);
            using MemoryStream data = new(GalleryThumbnailTestImages.CreatePngBytes((int)imageSize.Width, (int)imageSize.Height));
            using Bitmap bitmap = new(data);
            GenerationItemViewModel[] cards = CreateBranchChain(2);

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
            using GenerationChatBranchMapViewModel map = new(new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
            map.Open(cards, cards[0].Id, cards[0].Id);
            GenerationChatBranchMapView view = new(factory) { DataContext = map };
            Window window = DesktopControlTestBase.Show(view, 1100d, 850d);

            try
            {
                GenerationPreviewControl[] previews = cards.Select(card => view.GetVisualDescendants().OfType<GenerationPreviewControl>()
                    .Single(preview => ReferenceEquals(preview.DataContext, card))).ToArray();
                await WaitForFramesAsync(window, () =>
                {
                    frames.RunFrame(TimeSpan.Zero);
                    return previews.All(preview => ReferenceEquals(preview.FindControl<Image>("PreviewImage")?.Source, bitmap));
                });

                await verify(window, view, previews);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
