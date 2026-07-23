using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationMetadataOverlayViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Layout_WhenShown_UsesSharedModalAtRenderedSize()
    {
        Dispatch(() =>
        {
            string imagePath = CreateImageFile();

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                Window window = Show(view, 750d, 780d);

                try
                {
                    window.CaptureRenderedFrame();
                    ModalOverlayControl panel = view.FindControl<ModalOverlayControl>("PanelRoot")
                        ?? throw new InvalidOperationException("Metadata panel was not found.");
                    Button repeat = view.FindControl<Button>("RepeatEntry")
                        ?? throw new InvalidOperationException("Repeat button was not found.");
                    TextBlock title = panel
                        .GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Single(control => control.Classes.Contains("overlay-title"));
                    Rect repeatBounds = GetBounds(repeat, panel);
                    double contentBottom = panel.Bounds.Height - panel.Padding.Bottom;

                    panel.Width.Should().Be(560d);
                    panel.Height.Should().Be(588d);
                    repeatBounds.Bottom.Should().BeLessThanOrEqualTo(contentBottom);
                    title.FontWeight.Should().Be(FontWeight.Bold);
                    view.GetVisualDescendants().OfType<LayoutTransformControl>().Should().BeEmpty();
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
    public void Preview_WithPointerAndShift_ExpandsAboveMetadataContent()
    {
        Dispatch(() =>
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
                    Grid previewHost = preview.FindControl<Grid>("PreviewExpansionHost")
                        ?? throw new InvalidOperationException("Preview host was not found.");
                    Border prompt = view.FindControl<Border>("PromptEntry")
                        ?? throw new InvalidOperationException("Prompt panel was not found.");
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

                    previewHost.Width.Should().BeGreaterThan(100d);
                    previewHost.Height.Should().BeGreaterThan(100d);
                    preview.ZIndex.Should().Be(
                        GenerationPreviewExpansionVisualMetrics.ActiveZIndex);
                    Rect expandedBounds = GetBounds(previewHost, view);
                    Rect promptBounds = GetBounds(prompt, view);
                    expandedBounds.Intersects(promptBounds).Should().BeTrue();
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

    private static GenerationMetadataOverlayView CreateView(string imagePath)
    {
        GenerationItemDto itemDto = GenerationItemDtoTestFactory.Create(
            modelDisplayName: "X",
            prompt: "Промпт",
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
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
        GenerationMetadataViewModel viewModel = GenerationMetadataViewModel.FromItem(
            item,
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RecordingTextClipboardService(),
            new TestViewModelErrorHandler(),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter());

        return new GenerationMetadataOverlayView
        {
            DataContext = viewModel
        };
    }
}
