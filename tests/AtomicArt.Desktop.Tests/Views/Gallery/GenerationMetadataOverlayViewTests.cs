using System.Globalization;

using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using ShapePath = Avalonia.Controls.Shapes.Path;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationMetadataOverlayViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public async Task Navigation_WhenInputsRepeat_ShowsIntermediateEntriesAndKeepsHeightTransition()
    {
        await DispatchAsync(async () =>
        {
            using GalleryViewModel gallery = GalleryViewModelTestFactory.CreateViewModel();
            GenerationReference reference = new(new string('a', 64), "reference.png", GenerationImageContentTypes.Png);
            GalleryItemState[] items =
            [
                GalleryItemStateTestFactory.CreateGenerated(prompt: "First", secondsBefore: 0),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Second", secondsBefore: 1, references: [reference]),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Third", secondsBefore: 2),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Fourth", secondsBefore: 3),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Fifth", secondsBefore: 4, references: [reference])
            ];
            await gallery.RestoreStateAsync(items, CancellationToken.None);
            gallery.OpenMetadataCommand.Execute(gallery.Items[0]);
            TestUiFrameScheduler frameScheduler = new();
            GenerationMetadataOverlayView view = new()
            {
                DataContext = gallery.SelectedMetadata,
                NavigationFrameScheduler = frameScheduler
            };
            Window window = Show(view, 1040d, 640d);

            try
            {
                ModalOverlayControl panel = view.FindControl<ModalOverlayControl>("PanelRoot")!;
                Image previousPage = view.FindControl<Image>("PreviousMetadataPage")!;
                Grid currentBody = view.FindControl<Grid>("MetadataBody")!;
                TextBox prompt = view.FindControl<TextBox>("PromptText")!;
                double startHeight = panel.Bounds.Height;
                double startWidth = panel.Bounds.Width;
                previousPage.Source.Should().BeNull();
                currentBody.Opacity.Should().Be(1d);
                frameScheduler.HasQueuedFrame.Should().BeFalse();

                gallery.NavigateMetadataCommand.Execute(OverlayNavigationDirection.Next);
                double firstFrameHeight = RenderNavigationFrame(0);
                firstFrameHeight.Should().Be(startHeight);
                RenderTargetBitmap firstPage = previousPage.Source.Should().BeOfType<RenderTargetBitmap>().Subject;
                previousPage.Opacity.Should().Be(1d);
                currentBody.Opacity.Should().Be(0d);
                RenderNavigationFrame(1);
                double growingHeight = RenderNavigationFrame(40);
                growingHeight.Should().BeGreaterThan(firstFrameHeight);
                previousPage.Source.Should().BeSameAs(firstPage);
                previousPage.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                currentBody.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);

                gallery.NavigateMetadataCommand.Execute(OverlayNavigationDirection.Next);
                RenderNavigationFrame(41).Should().Be(growingHeight);
                previousPage.Source.Should().BeNull();
                currentBody.Opacity.Should().Be(1d);
                prompt.Text.Should().Be("Third");
                double shrinkingHeight = RenderNavigationFrame(81);
                shrinkingHeight.Should().BeInRange(startHeight + 1d, growingHeight - 1d);

                gallery.NavigateMetadataCommand.Execute(OverlayNavigationDirection.Next);
                RenderNavigationFrame(82).Should().Be(shrinkingHeight);
                previousPage.Source.Should().BeNull();
                currentBody.Opacity.Should().Be(1d);
                prompt.Text.Should().Be("Fourth");
                RenderNavigationFrame(300).Should().Be(startHeight);

                gallery.NavigateMetadataCommand.Execute(OverlayNavigationDirection.Next);
                RenderNavigationFrame(301).Should().Be(startHeight);
                previousPage.Opacity.Should().Be(1d);
                currentBody.Opacity.Should().Be(0d);
                RenderNavigationFrame(302);
                RenderNavigationFrame(391);
                previousPage.Source.Should().NotBeNull();
                RenderNavigationFrame(392);
                previousPage.Source.Should().BeNull();
                RenderNavigationFrame(421);
                currentBody.Opacity.Should().BeLessThan(1d);
                RenderNavigationFrame(422);
                currentBody.Opacity.Should().Be(1d);
                double finalHeight = RenderNavigationFrame(461);
                finalHeight.Should().BeGreaterThan(growingHeight);
                panel.Bounds.Width.Should().Be(startWidth);
                frameScheduler.HasQueuedFrame.Should().BeFalse();

                double RenderNavigationFrame(int milliseconds)
                {
                    frameScheduler.RunFrame(TimeSpan.FromMilliseconds(milliseconds));
                    window.CaptureRenderedFrame();
                    return panel.Bounds.Height;
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Navigation_WhenPreviewIsPending_HoldsPreviousPageUntilPreviewIsPresented()
    {
        await DispatchAsync(async () =>
        {
            using GalleryViewModel gallery = GalleryViewModelTestFactory.CreateViewModel();
            GalleryItemState[] items =
            [
                GalleryItemStateTestFactory.CreateGenerated(secondsBefore: 0),
                GalleryItemStateTestFactory.CreateGenerated(secondsBefore: 1, thumbnailPath: "second-thumbnail.png")
            ];
            await gallery.RestoreStateAsync(items, CancellationToken.None);
            gallery.OpenMetadataCommand.Execute(gallery.Items[0]);
            TestUiFrameScheduler navigationFrames = new();
            TestUiFrameScheduler previewFrames = new();
            TaskCompletionSource<GalleryPreviewBitmapLease?> decode = new();
            StubGalleryPreviewBitmapProvider provider = new((_, _) => decode.Task);
            GalleryPreviewSourceScheduler sourceScheduler = new(
                previewFrames, TestApiConfiguration.CreateGalleryOptionsWrapper());
            GenerationPreviewSessionFactory sessionFactory = new(
                _ => new GenerationPreviewSession(provider, sourceScheduler));
            GenerationMetadataOverlayView view = new(sessionFactory)
            {
                DataContext = gallery.SelectedMetadata,
                NavigationFrameScheduler = navigationFrames
            };
            Window window = Show(view, 1040d, 640d);

            try
            {
                Image previousPage = view.FindControl<Image>("PreviousMetadataPage")!;
                GenerationPreviewControl preview = view.FindControl<GenerationPreviewControl>("PreviewEntry")!;
                using MemoryStream image = new(GalleryThumbnailTestImages.CreatePngBytes(32, 32));
                using Bitmap bitmap = new(image);

                gallery.NavigateMetadataCommand.Execute(OverlayNavigationDirection.Next);
                navigationFrames.RunFrame(TimeSpan.Zero);
                navigationFrames.RunFrame(TimeSpan.FromMilliseconds(100d));
                previousPage.Opacity.Should().Be(1d);

                decode.SetResult(new GalleryPreviewBitmapLease(bitmap, () => { }));
                await Task.Yield();
                previewFrames.HasQueuedFrame.Should().BeTrue();
                navigationFrames.RunFrame(TimeSpan.FromMilliseconds(150d));
                previousPage.Opacity.Should().Be(1d);

                previewFrames.RunFrame(TimeSpan.FromMilliseconds(151d));
                await Task.Yield();
                preview.IsPreviewLoadPending.Should().BeFalse();
                navigationFrames.RunFrame(TimeSpan.FromMilliseconds(152d));
                navigationFrames.RunFrame(TimeSpan.FromMilliseconds(168d));
                navigationFrames.RunFrame(TimeSpan.FromMilliseconds(220d));

                previousPage.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(1200d, 128d)]
    [InlineData(1040d, 104d)]
    [InlineData(670d, 32d)]
    public async Task SideArrows_WhenPointerMovesOutsidePanel_AppearOnHoveredSideAndNavigate(
        double windowWidth,
        double triggerWidth)
    {
        await DispatchAsync(async () =>
        {
            using GalleryViewModel gallery = GalleryViewModelTestFactory.CreateViewModel();
            GalleryItemState[] items =
            [
                GalleryItemStateTestFactory.CreateGenerated(prompt: "First", secondsBefore: 0),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Second", secondsBefore: 1),
                GalleryItemStateTestFactory.CreateGenerated(prompt: "Third", secondsBefore: 2)
            ];
            await gallery.RestoreStateAsync(items, CancellationToken.None);
            gallery.OpenMetadataCommand.Execute(gallery.Items[1]);
            GenerationMetadataOverlayView view = new() { DataContext = gallery.SelectedMetadata };
            Window window = Show(view, windowWidth, 640d);

            try
            {
                Button previous = view.FindControl<Button>("PreviousGenerationButton")!;
                Button next = view.FindControl<Button>("NextGenerationButton")!;
                TextBox prompt = view.FindControl<TextBox>("PromptText")!;
                Border leftIcon = GetNavigationIcon(previous);
                Border rightIcon = GetNavigationIcon(next);
                Rect panel = GetBounds(view.FindControl<ModalOverlayControl>("PanelRoot")!, window);
                Rect previousBounds = GetBounds(previous, window);
                Rect nextBounds = GetBounds(next, window);
                Rect leftIconBounds = GetBounds(leftIcon, window);
                Rect rightIconBounds = GetBounds(rightIcon, window);

                previousBounds.Width.Should().Be(triggerWidth);
                nextBounds.Width.Should().Be(triggerWidth);
                previousBounds.Right.Should().Be(panel.Left);
                nextBounds.Left.Should().Be(panel.Right);
                leftIconBounds.Right.Should().Be(panel.Left);
                rightIconBounds.Left.Should().Be(panel.Right);
                AssertNavigationIconLayout(leftIcon, new CornerRadius(8d, 0d, 0d, 8d));
                AssertNavigationIconLayout(rightIcon, new CornerRadius(0d, 8d, 8d, 0d));
                window.MouseMove(new Point(panel.Left + 1d, panel.Center.Y), RawInputModifiers.None);
                leftIcon.Opacity.Should().Be(0d);
                rightIcon.Opacity.Should().Be(0d);

                window.MouseMove(new Point(previousBounds.Left + 1d, panel.Center.Y), RawInputModifiers.None);
                leftIcon.Opacity.Should().Be(1d);
                rightIcon.Opacity.Should().Be(0d);
                window.MouseMove(leftIconBounds.Center, RawInputModifiers.None);
                window.MouseDown(leftIconBounds.Center, MouseButton.Left);
                window.MouseUp(leftIconBounds.Center, MouseButton.Left);

                gallery.SelectedMetadata!.Item.Should().BeSameAs(gallery.Items[0]);
                prompt.Text.Should().Be("First");
                previous.IsEffectivelyEnabled.Should().BeFalse();
                leftIcon.Opacity.Should().Be(0d);

                window.MouseMove(new Point(nextBounds.Right - 1d, panel.Center.Y), RawInputModifiers.None);
                rightIcon.Opacity.Should().Be(1d);
                window.MouseMove(rightIconBounds.Center, RawInputModifiers.None);
                window.MouseDown(rightIconBounds.Center, MouseButton.Left);
                window.MouseUp(rightIconBounds.Center, MouseButton.Left);
                gallery.SelectedMetadata.Item.Should().BeSameAs(gallery.Items[1]);
                prompt.Text.Should().Be("Second");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Border GetNavigationIcon(Button button)
    {
        Border icon = button.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Name == "NavigationIconHost");
        icon.Transitions = null;
        return icon;
    }

    private static void AssertNavigationIconLayout(Border icon, CornerRadius expectedRadius)
    {
        ShapePath chevron = icon.GetVisualDescendants().OfType<ShapePath>()
            .Single(path => path.Name == "NavigationChevron");
        Geometry geometry = chevron.RenderedGeometry ?? throw new InvalidOperationException("Chevron has no geometry.");
        Point center = chevron.TranslatePoint(geometry.Bounds.Center, icon)
            ?? throw new InvalidOperationException("Chevron has no position.");

        icon.Bounds.Size.Should().Be(new Size(32d, 32d));
        icon.CornerRadius.Should().Be(expectedRadius);
        center.X.Should().BeApproximately(icon.Bounds.Width / 2d, 0.5d);
        center.Y.Should().BeApproximately(icon.Bounds.Height / 2d, 0.5d);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    public async Task References_WhenShown_MatchAttachmentTilesAndAlignWithReuse(int referenceCount)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationMetadataOverlayViewTests));
        try
        {
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new(
                "panorama.png", GenerationImageContentTypes.Png, GalleryThumbnailTestImages.CreatePngBytes(1024, 84));
            Guid runId = Guid.NewGuid();
            IReadOnlyList<GenerationReference> references = await store.SaveAsync(
                runId, Enumerable.Repeat(image, referenceCount).ToArray(), CancellationToken.None);
            store.Release(runId);
            GenerationReferencePreviewLoader loader = new(
                store, NullLogger<GenerationReferencePreviewLoader>.Instance);

            await DispatchAsync(async () =>
            {
                GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(references: references);
                GenerationItemViewModel item = GenerationItemViewModel.Restore(
                    state, state.ImagePath, state.ThumbnailPath,
                    GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
                GenerationReference? opened = null;
                AsyncRelayCommand<GenerationReference?> command = new(reference =>
                {
                    opened = reference;
                    return Task.CompletedTask;
                });
                Mock<IGenerationReferenceDragService> dragService = new();
                dragService.Setup(service => service.DragAsync(
                    It.IsAny<Control>(), It.IsAny<PointerPressedEventArgs>(),
                    It.IsAny<GenerationReference>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                GenerationMetadataOverlayView view = CreateView(
                    item, new RecordingGenerationPreviewSessionFactory(), loader, command, dragService.Object);
                Window window = Show(view);
                try
                {
                    GenerationReferenceButton button =
                        view.GetVisualDescendants().OfType<GenerationReferenceButton>().First();
                    button.DragService.Should().BeSameAs(dragService.Object);
                    Border tile = button.GetVisualDescendants().OfType<Border>()
                        .Single(control => control.Classes.Contains("attachment-preview"));
                    Button reuse = view.FindControl<Button>("RepeatEntry")!;
                    TextBlock heading = view.FindControl<TextBlock>("ReferencesHeading")!;
                    GenerationReferencePreviewControl preview = button.GetVisualDescendants()
                        .OfType<GenerationReferencePreviewControl>().Single();
                    TaskCompletionSource<Bitmap> loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    preview.PropertyChanged += (_, args) =>
                    {
                        if (args.Property == Image.SourceProperty && preview.Source is Bitmap bitmap)
                        {
                            loaded.TrySetResult(bitmap);
                        }
                    };

                    Bitmap thumbnail = preview.Source as Bitmap
                        ?? await loaded.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    Point position = GetBounds(button, window).Center;
                    window.MouseDown(position, MouseButton.Left);
                    window.MouseUp(position, MouseButton.Left);

                    thumbnail.PixelSize.Width.Should().BeInRange(1, 96);
                    thumbnail.PixelSize.Height.Should().BeInRange(1, 96);
                    opened.Should().Be(references[0]);
                    tile.Bounds.Size.Should().Be(new Size(56d, 56d));
                    tile.BorderThickness.Should().Be(default(Thickness));
                    GetBounds(tile, view).Bottom.Should().Be(GetBounds(reuse, view).Bottom);
                    (GetBounds(tile, view).Top - GetBounds(heading, view).Bottom).Should().Be(8d);
                    Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();

                    opened = null;
                    window.MouseDown(position, MouseButton.Left);
                    window.MouseMove(position + new Vector(2d, 0d), RawInputModifiers.LeftMouseButton);
                    dragService.VerifyNoOtherCalls();
                    window.MouseMove(position + new Vector(10d, 0d), RawInputModifiers.LeftMouseButton);
                    window.MouseUp(position + new Vector(10d, 0d), MouseButton.Left);

                    dragService.Verify(service => service.DragAsync(
                        button, It.IsAny<PointerPressedEventArgs>(), references[0], CancellationToken.None), Times.Once);
                    opened.Should().BeNull();
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task References_WhenScrolled_FadeOnlyWhereItemsExtendPastTheViewport()
    {
        await DispatchAsync(() =>
        {
            GenerationReference[] references = Enumerable.Range(0, 16)
                .Select(index => new GenerationReference("reference", $"reference-{index}.png", GenerationImageContentTypes.Png))
                .ToArray();
            GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(references: references);
            GenerationItemViewModel item = GenerationItemViewModel.Restore(
                state, state.ImagePath, state.ThumbnailPath,
                GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
            GenerationMetadataOverlayView view = CreateView(item);
            Window window = Show(view);

            try
            {
                ScrollViewer scroller = view.GetVisualDescendants().OfType<ScrollViewer>()
                    .Single(control => control.Classes.Contains("metadata-references"));
                ScrollContentPresenter presenter = scroller.GetVisualDescendants()
                    .OfType<ScrollContentPresenter>().Single();
                LinearGradientBrush mask = presenter.OpacityMask.Should().BeOfType<LinearGradientBrush>().Subject;
                double maximumOffset = scroller.Extent.Width - scroller.Viewport.Width;

                maximumOffset.Should().BeGreaterThan(40d);
                scroller.OpacityMask.Should().BeNull();
                mask.EndPoint.Should().Be(new RelativePoint(1d, 0d, RelativeUnit.Relative));
                mask.GradientStops[0].Color.A.Should().Be(byte.MaxValue);
                mask.GradientStops[3].Color.A.Should().Be(0);

                scroller.Offset = new Vector(10d, 0d);
                window.CaptureRenderedFrame();

                mask.GradientStops[0].Color.A.Should().Be(0);
                mask.GradientStops[1].Offset.Should().BeApproximately(10d / presenter.Bounds.Width, 0.001d);
                mask.GradientStops[3].Color.A.Should().Be(0);

                scroller.Offset = new Vector(maximumOffset, 0d);
                window.CaptureRenderedFrame();

                mask.GradientStops[0].Color.A.Should().Be(0);
                mask.GradientStops[3].Color.A.Should().Be(byte.MaxValue);

                scroller.GetVisualDescendants().OfType<ItemsControl>().Single().ItemsSource = references.Take(1);
                window.CaptureRenderedFrame();

                mask.GradientStops[0].Color.A.Should().Be(byte.MaxValue);
                mask.GradientStops[3].Color.A.Should().Be(byte.MaxValue);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ReferencePreview_WhenImageArrives_FadesIn()
    {
        string imagePath = CreateImageFile();
        try
        {
            await DispatchAsync(async () =>
            {
                using Bitmap bitmap = new(imagePath);
                GenerationReferencePreviewControl preview = new();
                Window window = Show(preview);
                try
                {
                    preview.Opacity.Should().Be(0d);
                    preview.Transitions.Should().ContainSingle()
                        .Which.Should().BeOfType<DoubleTransition>().Which.Property.Should().Be(Visual.OpacityProperty);
                    TaskCompletionSource visible = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    preview.PropertyChanged += (_, args) =>
                    {
                        if (args.Property == Visual.OpacityProperty && preview.Opacity == 1d)
                        {
                            visible.TrySetResult();
                        }
                    };

                    preview.Source = bitmap;

                    preview.Opacity.Should().BeLessThan(1d);
                    await visible.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    preview.Opacity.Should().Be(1d);
                    preview.Source = null;
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
    public async Task Background_WhenShown_UsesOpaqueGradientsForHeaderAndBody()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                Window window = Show(view);

                try
                {
                    window.CaptureRenderedFrame();
                    ModalOverlayControl panel = view.FindControl<ModalOverlayControl>("PanelRoot")
                        ?? throw new InvalidOperationException("Metadata panel was not found.");
                    Border header = panel
                        .GetVisualDescendants()
                        .OfType<Border>()
                        .Single(control => control.Name == "PART_Header");
                    Border status = header
                        .GetVisualDescendants()
                        .OfType<Border>()
                        .Single(control => control.Classes.Contains("metadata-status"));

                    AssertOpaqueGradient(panel.Background, "#1B2435", "#121B2C");
                    AssertOpaqueGradient(header.Background, "#293345", "#202A3C");
                    AssertBrushColor(status.Background, "#203B35");
                    panel.BlurRadius.Should().Be(0d);
                    panel.BorderThickness.Should().Be(default(Thickness));
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    [Theory]
    [InlineData(1024d, 768d, 832d)]
    [InlineData(670d, 340d, 606d)]
    public async Task Layout_WithLargeOrSmallWindow_KeepsPreviewAndRepeatInModal(
        double windowWidth,
        double windowHeight,
        double panelWidth)
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                Window window = Show(view, windowWidth, windowHeight);

                try
                {
                    window.CaptureRenderedFrame();
                    ModalOverlayControl panel = view.FindControl<ModalOverlayControl>("PanelRoot")
                        ?? throw new InvalidOperationException("Metadata panel was not found.");
                    Button repeat = view.FindControl<Button>("RepeatEntry")
                        ?? throw new InvalidOperationException("Repeat button was not found.");
                    Button close = panel.GetVisualDescendants()
                        .OfType<Button>()
                        .Single(button => button.Classes.Contains("close-action"));
                    Border status = panel.GetVisualDescendants()
                        .OfType<Border>()
                        .Single(border => border.Classes.Contains("metadata-status"));
                    GenerationPreviewControl preview =
                        view.FindControl<GenerationPreviewControl>("PreviewEntry")
                        ?? throw new InvalidOperationException("Preview was not found.");
                    TextBlock createdAt = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.CreatedAt);
                    Rect repeatBounds = GetBounds(repeat, panel);
                    Rect statusBounds = GetBounds(status, panel);

                    panel.Bounds.Width.Should().Be(panelWidth);
                    panel.Bounds.Height.Should().BeLessThan(Math.Min(windowHeight, 480d));
                    GetBounds(panel, view).Center.X.Should().BeApproximately(windowWidth / 2d, 1d);
                    repeatBounds.Bottom.Should().BeLessThan(panel.Bounds.Height);
                    repeatBounds.Right.Should().BeLessThan(panel.Bounds.Width);
                    close.IsVisible.Should().BeFalse();
                    (panel.Bounds.Width - statusBounds.Right).Should().BeApproximately(statusBounds.Top, 2d);
                    preview.Bounds.Width.Should().BeInRange(220d, 300d);
                    preview.Bounds.Height.Should().Be(preview.Bounds.Width);
                    double columnGap = GetBounds(createdAt, panel).Left - GetBounds(preview, panel).Right;
                    columnGap.Should().Be(24d);
                    AssertBrushColor(repeat.Background, "#B8D0FF");
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    [Theory]
    [InlineData(1024d)]
    [InlineData(670d)]
    public async Task MetadataFields_WithWideOrNarrowWindow_StayInOrder(double windowWidth)
    {
        await DispatchAsync(() =>
        {
            GenerationMetadataOverlayView view = CreateView("result.png");
            GenerationMetadataViewModel viewModel = (GenerationMetadataViewModel)view.DataContext!;
            string localTime = viewModel.Item.CreatedAtUtc
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

            Show(view, windowWidth, 768d, _ =>
            {
                TextBlock date = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.CreatedAt);
                TextBlock duration = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.GenerationDuration);
                TextBlock price = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.Price);
                TextBlock resolution = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.Resolution);
                TextBlock aspectRatio = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.AspectRatio);
                Run[] runs = date.Inlines!.OfType<Run>().ToArray();
                Rect dateBounds = GetBounds(date, view);
                Rect durationBounds = GetBounds(duration, view);
                Rect priceBounds = GetBounds(price, view);
                Rect resolutionBounds = GetBounds(resolution, view);
                Rect aspectRatioBounds = GetBounds(aspectRatio, view);

                string.Concat(runs.Select(run => run.Text)).Should().Be(localTime);
                runs[1].Foreground.Should().NotBe(date.Foreground);
                durationBounds.Top.Should().BeApproximately(dateBounds.Top, 1d);
                durationBounds.Left.Should().BeGreaterThan(dateBounds.Left);
                priceBounds.Top.Should().BeApproximately(dateBounds.Top, 1d);
                priceBounds.Left.Should().BeGreaterThan(durationBounds.Left);
                resolutionBounds.Top.Should().BeGreaterThan(dateBounds.Bottom);
                resolutionBounds.Left.Should().Be(dateBounds.Left);
                aspectRatioBounds.Top.Should().BeApproximately(resolutionBounds.Top, 1d);
                aspectRatioBounds.Left.Should().BeApproximately(durationBounds.Left, 1d);
            });
        });
    }

    [Fact]
    public async Task ModelName_WithPrice_KeepsFullNameOnOneLine()
    {
        await DispatchAsync(() =>
        {
            const string ModelName = "GPT Image 2.5 Sunburst";
            GenerationMetadataOverlayView view = CreateView("result.png");
            GenerationMetadataViewModel viewModel = (GenerationMetadataViewModel)view.DataContext!;
            viewModel.Item.ModelDisplayName = ModelName;

            Show(view, 1024d, 768d, _ =>
            {
                TextBlock model = view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(text => text.Classes.Contains("metadata-model"));
                TextBlock price = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.Price);

                model.Text.Should().Be(ModelName);
                model.FontSize.Should().Be(20d);
                model.TextLayout.TextLines.Count.Should().Be(1);
                GetBounds(price, view).Top.Should().BeGreaterThan(GetBounds(model, view).Bottom);
            });
        });
    }

    [Theory]
    [InlineData(1024d)]
    [InlineData(670d)]
    public async Task MetadataFields_WithSavedOptions_FillSecondRowBeforeWrapping(double windowWidth)
    {
        await DispatchAsync(() =>
        {
            GenerationRequestSettings settings = GenerationRequestSettingsTestFactory.Create(
                (GenerationParameterNames.Quality, "high"),
                (GenerationParameterNames.Complexity, "medium"));
            GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(requestSettings: settings);
            GenerationItemViewModel item = GenerationItemViewModel.Restore(
                state,
                state.ImagePath,
                state.ThumbnailPath,
                GenerationItemStatusDescriptorRegistryTestFactory.Create(),
                TestLocalizationTextProvider.Default);
            GenerationMetadataOverlayView view = CreateView(item);

            Show(view, windowWidth, 768d, _ =>
            {
                Rect price = GetBounds(GetMetadataValue(view, GalleryLocalizationKeys.Metadata.Price), view);
                Rect resolution = GetBounds(GetMetadataValue(view, GalleryLocalizationKeys.Metadata.Resolution), view);
                Rect quality = GetBounds(GetMetadataValue(view, GenerationUiLocalizationKeys.Quality.Label), view);
                Rect complexity = GetBounds(GetMetadataValue(view, GenerationUiLocalizationKeys.Complexity.Label), view);

                quality.Top.Should().BeApproximately(resolution.Top, 1d);
                quality.Left.Should().Be(price.Left);
                complexity.Top.Should().BeGreaterThan(quality.Bottom);
                complexity.Left.Should().Be(resolution.Left);
            });
        });
    }

    [Fact]
    public async Task Preview_WithPointerAndShift_ExpandsAboveMetadataContent()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                Window window = Show(view, 750d, 780d);

                try
                {
                    window.CaptureRenderedFrame();
                    GenerationPreviewControl preview = view
                        .GetVisualDescendants()
                        .OfType<GenerationPreviewControl>()
                        .Single();
                    using Bitmap bitmap = new(imagePath);
                    Image previewImage = preview.FindControl<Image>("PreviewImage")
                        ?? throw new InvalidOperationException("Preview image was not found.");
                    previewImage.Source = bitmap;
                    Grid previewHost = preview.FindControl<Grid>("PreviewExpansionHost")
                        ?? throw new InvalidOperationException("Preview host was not found.");
                    previewHost.Transitions = null;
                    TextBlock date = GetMetadataValue(view, GalleryLocalizationKeys.Metadata.CreatedAt);
                    Point? previewPosition = preview.TranslatePoint(new Point(0d, 0d), window);
                    previewPosition.Should().NotBeNull();

                    window.MouseMove(
                        previewPosition.Value + new Vector(50d, 50d),
                        RawInputModifiers.None);
                    window.KeyPress(
                        Key.LeftShift,
                        RawInputModifiers.Shift,
                        PhysicalKey.ShiftLeft,
                        null);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().BeGreaterThan(preview.PreviewSize);
                    previewHost.Height.Should().BeGreaterThan(preview.PreviewSize);
                    preview.ZIndex.Should().Be(
                        GenerationPreviewExpansionVisualMetrics.ActiveZIndex);
                    Rect expandedBounds = GetBounds(previewHost, view);
                    Rect dateBounds = GetBounds(date, view);
                    expandedBounds.Intersects(dateBounds).Should().BeTrue(
                        "the expanded preview at {0} should overlap the metadata column at {1}",
                        expandedBounds,
                        dateBounds);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    private static void AssertOpaqueGradient(IBrush? brush, string startColor, string endColor)
    {
        ILinearGradientBrush gradient = brush.Should().BeAssignableTo<ILinearGradientBrush>().Subject;
        gradient.GradientStops.Select(stop => stop.Color)
            .Should().Equal(Color.Parse(startColor), Color.Parse(endColor));
        gradient.Opacity.Should().Be(1d);
    }

    private static void AssertBrushColor(IBrush? brush, string hex)
    {
        ISolidColorBrush solidBrush = brush.Should().BeAssignableTo<ISolidColorBrush>().Subject;
        solidBrush.Color.Should().Be(Color.Parse(hex));
        solidBrush.Opacity.Should().Be(1d);
    }

    [Fact]
    public async Task PromptText_WhenScrolled_UsesFullViewportAndHalfWheelStep()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();
            string prompt = string.Join(
                Environment.NewLine,
                Enumerable.Range(1, 20).Select(lineNumber => $"Line {lineNumber}"));

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath, prompt);
                Window window = Show(view, 750d, 780d);

                try
                {
                    TextBox promptTextBox = view.FindControl<TextBox>("PromptText")
                        ?? throw new InvalidOperationException("Prompt text box was not found.");
                    promptTextBox.TryFindResource(
                            "MetadataTextScrollInsets",
                            out object? paddingResource)
                        .Should()
                        .BeTrue();
                    Thickness themePadding = paddingResource
                        .Should()
                        .BeOfType<Thickness>()
                        .Subject;
                    TextBoxScrollContentAssertions.AssertUsesScrollableInsets(
                        promptTextBox,
                        window,
                        themePadding);
                    ScrollViewer promptScrollViewer = promptTextBox.GetVisualDescendants()
                        .OfType<ScrollViewer>()
                        .Single();
                    SmoothScrollBehavior.GetWheelMultiplier(promptScrollViewer)
                        .Should().Be(48d);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    [Fact]
    public async Task PromptText_WhenShown_AlignsWithHeadingAndKeepsVerticalScrollFade()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                Window window = Show(view, 750d, 780d);

                try
                {
                    TextBlock heading = view.FindControl<TextBlock>("PromptHeading")
                        ?? throw new InvalidOperationException("Prompt heading was not found.");
                    TextBox textBox = view.FindControl<TextBox>("PromptText")
                        ?? throw new InvalidOperationException("Prompt text box was not found.");
                    ScrollContentPresenter scrollPresenter = textBox
                        .GetVisualDescendants()
                        .OfType<ScrollContentPresenter>()
                        .Single();
                    Control textPresenter = TextBoxScrollContentAssertions.GetTextPresenter(textBox);
                    textBox.TryFindResource("MetadataTextScrollInsets", out object? paddingResource)
                        .Should().BeTrue();
                    Thickness fadeInsets = paddingResource.Should().BeOfType<Thickness>().Subject;

                    GetBounds(textPresenter, view).Left.Should().Be(GetBounds(heading, view).Left);
                    GetBounds(textPresenter, view).Right.Should().Be(GetBounds(textBox, view).Right);
                    textPresenter.Margin.Should().Be(fadeInsets);
                    ScrollFadeMaskBehavior.GetInsets(scrollPresenter).Should().Be(fadeInsets);
                    scrollPresenter.OpacityMask.Should().BeOfType<LinearGradientBrush>();
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    [Fact]
    public async Task PromptHeading_WhenHovered_RevealsCopyButton()
    {
        await DispatchAsync(() =>
        {
            GenerationMetadataOverlayView view = CreateView("result.png");
            TextBlock heading = view.FindControl<TextBlock>("PromptHeading")
                ?? throw new InvalidOperationException("Prompt heading was not found.");
            Button copy = view.FindControl<Button>("PromptCopyButton")
                ?? throw new InvalidOperationException("Prompt copy button was not found.");
            heading.Text = "Prompt";

            Show(view, 1024d, 768d, window =>
            {
                copy.IsHitTestVisible.Should().BeFalse();

                window.MouseMove(GetBounds(heading, window).Center, RawInputModifiers.None);

                copy.IsHitTestVisible.Should().BeTrue();
            });
        });
    }

    [Fact]
    public async Task Preview_WhenOpenedAndResized_UsesFinalSizeWithoutTransition()
    {
        await DispatchAsync(() =>
        {
            GenerationMetadataOverlayView view = CreateView("result.png");
            GenerationPreviewControl preview = view.FindControl<GenerationPreviewControl>("PreviewEntry")
                ?? throw new InvalidOperationException("Preview was not found.");
            Grid host = preview.FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("Preview expansion host was not found.");

            Show(view, 1024d, 768d, window =>
            {
                Size openedSize = preview.Bounds.Size;
                host.Transitions.Should().BeNull();
                host.Bounds.Size.Should().Be(openedSize);

                window.Width = 670d;
                window.CaptureRenderedFrame();

                preview.Bounds.Width.Should().BeLessThan(openedSize.Width);
                host.Bounds.Size.Should().Be(preview.Bounds.Size);
            });
        });
    }

    [Fact]
    public async Task Preview_WhenShown_AttachesLoadingSessionAndBindsThumbnailPath()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                RecordingGenerationPreviewSessionFactory previewSessionFactory =
                    new();
                GenerationMetadataOverlayView view = CreateView(
                    imagePath,
                    previewSessionFactory: previewSessionFactory);
                Window window = Show(view, 750d, 780d);

                try
                {
                    GenerationPreviewControl preview = view
                        .GetVisualDescendants()
                        .OfType<GenerationPreviewControl>()
                        .Single();

                    preview.PreviewPath.Should().Be(imagePath);
                    previewSessionFactory.CreatedTopLevel.Should()
                        .BeSameAs(window);
                    previewSessionFactory.Session.PreviewControl.Should()
                        .BeSameAs(preview);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    [Fact]
    public async Task Preview_WhenClosed_DisposesLoadingSession()
    {
        await DispatchAsync(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                RecordingGenerationPreviewSessionFactory previewSessionFactory =
                    new();
                GenerationMetadataOverlayView view = CreateView(
                    imagePath,
                    previewSessionFactory: previewSessionFactory);
                Window window = Show(view, 750d, 780d);

                window.Close();

                previewSessionFactory.Session.DisposeCount.Should().Be(1);
            }
            finally
            {
                File.Delete(imagePath);
            }
        });
    }

    private static TextBlock GetMetadataValue(GenerationMetadataOverlayView view, string labelKey)
    {
        string label = TestLocalizationTextProvider.Default.Get(labelKey);
        return view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Classes.Contains("metadata-value")
                && text.DataContext is GenerationMetadataField field
                && field.Label == label);
    }

    private static Rect GetBounds(Control control, Control relativeTo)
    {
        Point? position = control.TranslatePoint(new Point(0d, 0d), relativeTo);

        return new Rect(
            position ?? throw new InvalidOperationException("Control position was not resolved."),
            control.Bounds.Size);
    }

    private static string CreateImageFile()
    {
        string imagePath = Path.Combine(
            Path.GetTempPath(),
            $"atomic-art-metadata-preview-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(440, 220));

        return imagePath;
    }

    private static GenerationMetadataOverlayView CreateView(
        string imagePath,
        string prompt = "Prompt",
        IGenerationPreviewSessionFactory? previewSessionFactory = null)
    {
        GenerationItemDto itemDto = GenerationItemDtoTestFactory.Create(
            modelDisplayName: "X",
            prompt: prompt,
            aspectRatio: GenerationAspectRatios.Auto,
            resolution: "1K",
            createdAtUtc: new DateTime(2026, 7, 17, 8, 32, 0, DateTimeKind.Utc),
            generationDuration: TimeSpan.FromSeconds(20),
            price: new GenerationPriceDto(
                0.3261m,
                "USD",
                GenerationPriceSources.ActualProviderUsage),
            imagePath: imagePath);
        GenerationItemViewModel item = new(
            itemDto,
            2,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default)
        {
            ThumbnailPath = imagePath
        };

        return CreateView(item, previewSessionFactory);
    }

    private static GenerationMetadataOverlayView CreateView(
        GenerationItemViewModel item,
        IGenerationPreviewSessionFactory? previewSessionFactory = null,
        GenerationReferencePreviewLoader? referencePreviewLoader = null,
        IAsyncRelayCommand<GenerationReference?>? openReferenceCommand = null,
        IGenerationReferenceDragService? referenceDragService = null)
    {
        GenerationMetadataViewModel viewModel = GenerationMetadataViewModel.FromItem(
            item,
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RecordingTextClipboardService(),
            new TestViewModelErrorHandler(),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter(TestLocalizationTextProvider.Default),
            TestLocalizationTextProvider.Default,
            openReferenceCommand);
        GenerationMetadataOverlayView view = previewSessionFactory is null
            ? new GenerationMetadataOverlayView()
            : new GenerationMetadataOverlayView(previewSessionFactory, referencePreviewLoader, referenceDragService);
        view.DataContext = viewModel;

        return view;
    }

    private sealed class RecordingGenerationPreviewSessionFactory
        : IGenerationPreviewSessionFactory
    {
        public RecordingGenerationPreviewSession Session { get; } = new();
        public TopLevel? CreatedTopLevel { get; private set; }

        public IGenerationPreviewSession Create(TopLevel topLevel)
        {
            CreatedTopLevel = topLevel;

            return Session;
        }
    }

    private sealed class RecordingGenerationPreviewSession
        : IGenerationPreviewSession
    {
        public GenerationPreviewControl? PreviewControl { get; private set; }
        public int DisposeCount { get; private set; }

        public void Attach(GenerationPreviewControl previewControl)
        {
            PreviewControl = previewControl;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
