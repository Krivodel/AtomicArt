using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class GenerationPreviewExpansionTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void ModifierHoverAndScroll_WithGeneratedImage_KeepsPreviewAttachedToCard()
    {
        Dispatch(() =>
        {
            using PreviewTestContext context = CreateScenarioWithImage("atomic-art-preview-expansion");

            AssertPreviewIsOpaque(context);
            Panel originalParent = context.PreviewHost.Parent as Panel
                ?? throw new InvalidOperationException("Generation preview parent was not found.");
            ScrollContentPresenter scrollPresenter = context.ScrollViewer
                .GetVisualDescendants()
                .OfType<ScrollContentPresenter>()
                .Single();
            bool originalScrollViewerClipToBounds = context.ScrollViewer.ClipToBounds;
            object? originalScrollViewerClip = context.ScrollViewer.Clip;
            bool originalPresenterClipToBounds = scrollPresenter.ClipToBounds;
            Point previewCenter = GetPointerPosition(context, 110d);
            originalScrollViewerClipToBounds.Should().BeFalse();
            originalScrollViewerClip.Should().BeNull();

                    context.Window.MouseMove(previewCenter, RawInputModifiers.Shift);
                    context.Window.CaptureRenderedFrame();

                    AssertExpandedPreviewAttachedToCard(
                        context,
                        originalParent,
                        originalScrollViewerClipToBounds,
                        originalScrollViewerClip);
                    AssertPreviewIsOpaque(context);
                    scrollPresenter.ClipToBounds.Should().BeFalse();
                    context.Card.ZIndex.Should().Be(1001);

                    bool allPreviewAncestorsAllowOverflow = context.PreviewHost
                        .GetVisualAncestors()
                        .TakeWhile(visual => !ReferenceEquals(visual, context.ScrollViewer))
                        .All(visual => visual is { ClipToBounds: false, Clip: null });
                    allPreviewAncestorsAllowOverflow.Should().BeTrue();

                    context.ScrollViewer.Offset = new Vector(0d, 40d);
                    context.Window.CaptureRenderedFrame();

                    AssertExpandedPreviewAttachedToCard(
                        context,
                        originalParent,
                        originalScrollViewerClipToBounds,
                        originalScrollViewerClip);

                    Point? viewportPosition = context.ScrollViewer.TranslatePoint(
                        new Point(0d, 0d),
                        context.Window);
                    viewportPosition.Should().NotBeNull();
                    Point pointerOverInfo = new(
                        previewCenter.X,
                        viewportPosition.Value.Y + 200d);
                    context.Window.MouseMove(pointerOverInfo, RawInputModifiers.Shift);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Width.Should().Be(220d);
                    context.Card.ZIndex.Should().Be(1000);
                    AssertPreviewIsOpaque(context);

                    context.ScrollViewer.Offset = new Vector(0d, 0d);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Width.Should().Be(748d);
                    context.Card.ZIndex.Should().Be(1001);
                    AssertPreviewIsOpaque(context);

                    context.Window.Content = null;
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Parent.Should().BeSameAs(originalParent);
                    context.ScrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    context.ScrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
                    scrollPresenter.ClipToBounds.Should().Be(originalPresenterClipToBounds);
        });
    }

    [Fact]
    public void ModifierPressedAfterScroll_WithPointerOverPreview_ExpandsWithoutPointerMovement()
    {
        Dispatch(() =>
        {
            using PreviewTestContext context = CreateScenarioWithImage("atomic-art-preview-modifier-after-scroll");

            AssertPreviewIsOpaque(context);
            Point pointerPosition = GetPointerPosition(context, 150d);

                    context.Window.MouseMove(pointerPosition, RawInputModifiers.None);
                    context.ScrollViewer.Offset = new Vector(0d, 40d);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Width.Should().Be(220d);

                    context.Window.KeyPress(
                        Key.LeftShift,
                        RawInputModifiers.Shift,
                        PhysicalKey.ShiftLeft,
                        null);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Width.Should().Be(748d);
                    AssertPreviewIsOpaque(context);
        });
    }

    private static PreviewTestContext CreateScenarioWithImage(string fileNamePrefix)
    {
        string imagePath = Path.Combine(
            Path.GetTempPath(),
            $"{fileNamePrefix}-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(440, 220));

        return CreateScenario(imagePath);
    }

    private static PreviewTestContext CreateScenario(string imagePath)
    {
        GenerationItemViewModel item = CreateItem();
        item.ImagePath = imagePath;
        ObservableCollection<GenerationItemViewModel> items = [item];
        AnimatedGalleryControl gallery = new(CreateSceneFactory())
        {
            Items = items
        };
        Window window = Show(gallery, 640d, 260d);
        GenerationCardControl card = GetGalleryPanel(gallery)
            .Children
            .OfType<GenerationCardControl>()
            .Single();
        GenerationPreviewControl preview = card
            .GetVisualDescendants()
            .OfType<GenerationPreviewControl>()
            .Single();
        Grid previewHost = card.FindControl<Grid>("PreviewExpansionHost")
            ?? throw new InvalidOperationException("Generation preview host was not found.");
        Image previewImage = preview.FindControl<Image>("PreviewImage")
            ?? throw new InvalidOperationException("Generation preview image was not found.");
        ScrollViewer scrollViewer = GetGalleryScrollViewer(gallery);

        return new PreviewTestContext(
            gallery,
            window,
            card,
            preview,
            previewHost,
            previewImage,
            scrollViewer,
            imagePath);
    }

    private static void AssertPreviewIsOpaque(PreviewTestContext context)
    {
        context.Card.Opacity.Should().Be(1d);
        context.Preview.ZIndex.Should().Be(10);
        context.Preview.Opacity.Should().Be(1d);
        context.PreviewImage.Opacity.Should().Be(1d);
    }

    private static void AssertExpandedPreviewAttachedToCard(
        PreviewTestContext context,
        Panel originalParent,
        bool originalScrollViewerClipToBounds,
        object? originalScrollViewerClip)
    {
        context.PreviewHost.Parent.Should().BeSameAs(originalParent);
        GetOverlayCanvas(context.Gallery).Children.Should().NotContain(context.PreviewHost);
        context.PreviewHost.Width.Should().Be(748d);
        context.ScrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
        context.ScrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
    }

    private static Point GetPointerPosition(PreviewTestContext context, double verticalOffset)
    {
        Point? cardPosition = context.Card.TranslatePoint(
            new Point(0d, 0d),
            context.Window);
        cardPosition.Should().NotBeNull();

        return cardPosition.Value + new Vector(110d, verticalOffset);
    }

    private sealed class PreviewTestContext : IDisposable
    {
        public AnimatedGalleryControl Gallery { get; }
        public Window Window { get; }
        public GenerationCardControl Card { get; }
        public GenerationPreviewControl Preview { get; }
        public Grid PreviewHost { get; }
        public Image PreviewImage { get; }
        public ScrollViewer ScrollViewer { get; }

        private readonly string _imagePath;

        public PreviewTestContext(
            AnimatedGalleryControl gallery,
            Window window,
            GenerationCardControl card,
            GenerationPreviewControl preview,
            Grid previewHost,
            Image previewImage,
            ScrollViewer scrollViewer,
            string imagePath)
        {
            Gallery = gallery;
            Window = window;
            Card = card;
            Preview = preview;
            PreviewHost = previewHost;
            PreviewImage = previewImage;
            ScrollViewer = scrollViewer;
            _imagePath = imagePath ?? throw new ArgumentNullException(nameof(imagePath));
        }

        public void Dispose()
        {
            Window.Close();
            File.Delete(_imagePath);
        }
    }

}
