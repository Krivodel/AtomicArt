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
        string imagePath = Path.Combine(
            Path.GetTempPath(),
            $"atomic-art-preview-expansion-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(440, 220));

        try
        {
            Dispatch(() =>
            {
                GenerationItemViewModel item = CreateItem();
                item.ImagePath = imagePath;
                ObservableCollection<GenerationItemViewModel> items = [item];
                AnimatedGalleryControl gallery = new(CreateSceneFactory())
                {
                    Items = items
                };
                Window window = Show(gallery, 640d, 260d);

                try
                {
                    GenerationCardControl card = GetGalleryPanel(gallery)
                        .Children
                        .OfType<GenerationCardControl>()
                        .Single();
                    Grid previewHost = card.FindControl<Grid>("PreviewExpansionHost")
                        ?? throw new InvalidOperationException("Generation preview host was not found.");
                    Panel originalParent = previewHost.Parent as Panel
                        ?? throw new InvalidOperationException("Generation preview parent was not found.");
                    ScrollViewer scrollViewer = GetGalleryScrollViewer(gallery);
                    ScrollContentPresenter scrollPresenter = scrollViewer
                        .GetVisualDescendants()
                        .OfType<ScrollContentPresenter>()
                        .Single();
                    bool originalScrollViewerClipToBounds = scrollViewer.ClipToBounds;
                    object? originalScrollViewerClip = scrollViewer.Clip;
                    bool originalPresenterClipToBounds = scrollPresenter.ClipToBounds;
                    Point? cardPosition = card.TranslatePoint(new Point(0d, 0d), window);
                    cardPosition.Should().NotBeNull();
                    Point previewCenter = cardPosition.Value + new Vector(110d, 110d);
                    originalScrollViewerClipToBounds.Should().BeFalse();
                    originalScrollViewerClip.Should().BeNull();

                    window.MouseMove(previewCenter, RawInputModifiers.Shift);
                    window.CaptureRenderedFrame();

                    previewHost.Parent.Should().BeSameAs(originalParent);
                    GetOverlayCanvas(gallery).Children.Should().NotContain(previewHost);
                    previewHost.Width.Should().Be(440d);
                    scrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    scrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
                    scrollPresenter.ClipToBounds.Should().BeFalse();
                    card.ZIndex.Should().Be(1001);

                    bool allPreviewAncestorsAllowOverflow = previewHost
                        .GetVisualAncestors()
                        .TakeWhile(visual => !ReferenceEquals(visual, scrollViewer))
                        .All(visual => visual is { ClipToBounds: false, Clip: null });
                    allPreviewAncestorsAllowOverflow.Should().BeTrue();

                    scrollViewer.Offset = new Vector(0d, 40d);
                    window.CaptureRenderedFrame();

                    previewHost.Parent.Should().BeSameAs(originalParent);
                    GetOverlayCanvas(gallery).Children.Should().NotContain(previewHost);
                    previewHost.Width.Should().Be(440d);
                    scrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    scrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);

                    Point? viewportPosition = scrollViewer.TranslatePoint(
                        new Point(0d, 0d),
                        window);
                    viewportPosition.Should().NotBeNull();
                    Point pointerOverInfo = new(
                        previewCenter.X,
                        viewportPosition.Value.Y + 200d);
                    window.MouseMove(pointerOverInfo, RawInputModifiers.Shift);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().Be(220d);
                    card.ZIndex.Should().Be(1000);

                    scrollViewer.Offset = new Vector(0d, 0d);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().Be(440d);
                    card.ZIndex.Should().Be(1001);

                    window.Content = null;
                    window.CaptureRenderedFrame();

                    previewHost.Parent.Should().BeSameAs(originalParent);
                    scrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    scrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
                    scrollPresenter.ClipToBounds.Should().Be(originalPresenterClipToBounds);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ModifierPressedAfterScroll_WithPointerOverPreview_ExpandsWithoutPointerMovement()
    {
        string imagePath = Path.Combine(
            Path.GetTempPath(),
            $"atomic-art-preview-modifier-after-scroll-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(440, 220));

        try
        {
            Dispatch(() =>
            {
                GenerationItemViewModel item = CreateItem();
                item.ImagePath = imagePath;
                AnimatedGalleryControl gallery = new(CreateSceneFactory())
                {
                    Items = new ObservableCollection<GenerationItemViewModel> { item }
                };
                Window window = Show(gallery, 640d, 260d);

                try
                {
                    GenerationCardControl card = GetGalleryPanel(gallery)
                        .Children
                        .OfType<GenerationCardControl>()
                        .Single();
                    Grid previewHost = card.FindControl<Grid>("PreviewExpansionHost")
                        ?? throw new InvalidOperationException("Generation preview host was not found.");
                    ScrollViewer scrollViewer = GetGalleryScrollViewer(gallery);
                    Point? cardPosition = card.TranslatePoint(new Point(0d, 0d), window);
                    cardPosition.Should().NotBeNull();
                    Point pointerPosition = cardPosition.Value + new Vector(110d, 150d);

                    window.MouseMove(pointerPosition, RawInputModifiers.None);
                    scrollViewer.Offset = new Vector(0d, 40d);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().Be(220d);

                    window.KeyPress(
                        Key.LeftShift,
                        RawInputModifiers.Shift,
                        PhysicalKey.ShiftLeft,
                        null);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().Be(440d);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

}
