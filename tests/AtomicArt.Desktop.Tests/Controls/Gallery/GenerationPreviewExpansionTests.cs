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
                PreviewTestContext context = CreateScenario(imagePath);

                try
                {
                    Panel originalParent = context.PreviewHost.Parent as Panel
                        ?? throw new InvalidOperationException("Generation preview parent was not found.");
                    ScrollContentPresenter scrollPresenter = context.ScrollViewer
                        .GetVisualDescendants()
                        .OfType<ScrollContentPresenter>()
                        .Single();
                    bool originalScrollViewerClipToBounds = context.ScrollViewer.ClipToBounds;
                    object? originalScrollViewerClip = context.ScrollViewer.Clip;
                    bool originalPresenterClipToBounds = scrollPresenter.ClipToBounds;
                    Point? cardPosition = context.Card.TranslatePoint(
                        new Point(0d, 0d),
                        context.Window);
                    cardPosition.Should().NotBeNull();
                    Point previewCenter = cardPosition.Value + new Vector(110d, 110d);
                    originalScrollViewerClipToBounds.Should().BeFalse();
                    originalScrollViewerClip.Should().BeNull();

                    context.Window.MouseMove(previewCenter, RawInputModifiers.Shift);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Parent.Should().BeSameAs(originalParent);
                    GetOverlayCanvas(context.Gallery).Children.Should().NotContain(context.PreviewHost);
                    context.PreviewHost.Width.Should().Be(748d);
                    context.ScrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    context.ScrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
                    scrollPresenter.ClipToBounds.Should().BeFalse();
                    context.Card.ZIndex.Should().Be(1001);

                    bool allPreviewAncestorsAllowOverflow = context.PreviewHost
                        .GetVisualAncestors()
                        .TakeWhile(visual => !ReferenceEquals(visual, context.ScrollViewer))
                        .All(visual => visual is { ClipToBounds: false, Clip: null });
                    allPreviewAncestorsAllowOverflow.Should().BeTrue();

                    context.ScrollViewer.Offset = new Vector(0d, 40d);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Parent.Should().BeSameAs(originalParent);
                    GetOverlayCanvas(context.Gallery).Children.Should().NotContain(context.PreviewHost);
                    context.PreviewHost.Width.Should().Be(748d);
                    context.ScrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    context.ScrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);

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

                    context.ScrollViewer.Offset = new Vector(0d, 0d);
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Width.Should().Be(748d);
                    context.Card.ZIndex.Should().Be(1001);

                    context.Window.Content = null;
                    context.Window.CaptureRenderedFrame();

                    context.PreviewHost.Parent.Should().BeSameAs(originalParent);
                    context.ScrollViewer.ClipToBounds.Should().Be(originalScrollViewerClipToBounds);
                    context.ScrollViewer.Clip.Should().BeSameAs(originalScrollViewerClip);
                    scrollPresenter.ClipToBounds.Should().Be(originalPresenterClipToBounds);
                }
                finally
                {
                    context.Window.Close();
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
                PreviewTestContext context = CreateScenario(imagePath);

                try
                {
                    Point? cardPosition = context.Card.TranslatePoint(
                        new Point(0d, 0d),
                        context.Window);
                    cardPosition.Should().NotBeNull();
                    Point pointerPosition = cardPosition.Value + new Vector(110d, 150d);

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
                }
                finally
                {
                    context.Window.Close();
                }
            });
        }
        finally
        {
            File.Delete(imagePath);
        }
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
        Grid previewHost = card.FindControl<Grid>("PreviewExpansionHost")
            ?? throw new InvalidOperationException("Generation preview host was not found.");
        ScrollViewer scrollViewer = GetGalleryScrollViewer(gallery);

        return new PreviewTestContext(
            gallery,
            window,
            card,
            previewHost,
            scrollViewer);
    }

    private sealed record PreviewTestContext(
        AnimatedGalleryControl Gallery,
        Window Window,
        GenerationCardControl Card,
        Grid PreviewHost,
        ScrollViewer ScrollViewer);

}
