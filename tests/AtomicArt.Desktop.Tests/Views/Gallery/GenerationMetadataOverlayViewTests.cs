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
    public void Preview_WithPointerAndShift_ExpandsOutsideThumbnail()
    {
        Dispatch(() =>
        {
            string imagePath = Path.Combine(
                Path.GetTempPath(),
                $"atomic-art-metadata-preview-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(
                imagePath,
                GalleryThumbnailTestImages.CreatePngBytes(440, 220));

            try
            {
                GenerationMetadataOverlayView view = CreateView(imagePath);
                GenerationPreviewControl preview = view
                    .GetVisualDescendants()
                    .OfType<GenerationPreviewControl>()
                    .Single();
                preview.ZIndex.Should().Be(10);
                AssertPreviewOpacityChainIsOpaque(preview, view);
                Window window = Show(view, 750d, 780d);

                try
                {
                    AssertPreviewOpacityChainIsOpaque(preview, view);
                    Thread.Sleep(40);
                    window.CaptureRenderedFrame();
                    AssertPreviewOpacityChainIsOpaque(preview, view);
                    Thread.Sleep(400);
                    window.CaptureRenderedFrame();
                    AssertPreviewOpacityChainIsOpaque(preview, view);
                    Border panel = view.FindControl<Border>("PanelRoot")
                        ?? throw new InvalidOperationException("Metadata panel was not found.");
                    Border prompt = view.FindControl<Border>("PromptEntry")
                        ?? throw new InvalidOperationException("Prompt panel was not found.");
                    Border path = view.FindControl<Border>("PathEntry")
                        ?? throw new InvalidOperationException("Path panel was not found.");
                    Border price = view.FindControl<Border>("PriceEntry")
                        ?? throw new InvalidOperationException("Price panel was not found.");
                    StackPanel parameters = view.FindControl<StackPanel>("ParametersEntry")
                        ?? throw new InvalidOperationException("Parameter panel was not found.");
                    Grid content = view.FindControl<Grid>("ContentGrid")
                        ?? throw new InvalidOperationException("Metadata content grid was not found.");
                    Button promptCopyButton = view.FindControl<Button>("PromptCopyButton")
                        ?? throw new InvalidOperationException("Prompt copy button was not found.");
                    Button pathCopyButton = view.FindControl<Button>("PathCopyButton")
                        ?? throw new InvalidOperationException("Path copy button was not found.");
                    TextBox promptText = view.FindControl<TextBox>("PromptText")
                        ?? throw new InvalidOperationException("Prompt text was not found.");
                    TextBox pathText = view.FindControl<TextBox>("PathText")
                        ?? throw new InvalidOperationException("Path text was not found.");
                    Button repeat = view.FindControl<Button>("RepeatEntry")
                        ?? throw new InvalidOperationException("Repeat button was not found.");
                    Grid previewHost = preview.FindControl<Grid>("PreviewExpansionHost")
                        ?? throw new InvalidOperationException("Preview host was not found.");
                    panel.Width.Should().Be(700d);
                    panel.Height.Should().Be(734d);
                    Point? panelTopLeft = panel.TranslatePoint(
                        new Point(0d, 0d),
                        window);
                    Point? panelBottomRight = panel.TranslatePoint(
                        new Point(panel.Bounds.Width, panel.Bounds.Height),
                        window);
                    panelTopLeft.Should().NotBeNull();
                    panelBottomRight.Should().NotBeNull();
                    Vector renderedPanelSize = panelBottomRight.Value - panelTopLeft.Value;
                    renderedPanelSize.X.Should().BeApproximately(560d, 0.1d);
                    renderedPanelSize.Y.Should().BeApproximately(587.2d, 0.1d);
                    double.IsNaN(price.Width).Should().BeTrue();
                    price.Bounds.Width.Should().BeLessThan(139d);
                    Border[] parameterChips = parameters.Children
                        .OfType<Border>()
                        .ToArray();
                    double.IsNaN(parameterChips[0].Width).Should().BeTrue();
                    parameterChips[0].Bounds.Width.Should().BeLessThan(228d);
                    parameterChips
                        .Skip(1)
                        .Select(chip => chip.Width)
                        .Should()
                        .Equal(94d, 117d, 80d);
                    promptText.IsReadOnly.Should().BeTrue();
                    pathText.IsReadOnly.Should().BeTrue();
                    promptText.SelectionStart = 0;
                    promptText.SelectionEnd = 3;
                    promptText.SelectedText.Should().Be("Про");
                    pathText.SelectionStart = 0;
                    pathText.SelectionEnd = 3;
                    pathText.SelectedText.Should().Be(imagePath[..3]);
                    promptCopyButton.IsHitTestVisible.Should().BeFalse();
                    pathCopyButton.IsHitTestVisible.Should().BeFalse();
                    ToolTip.GetTip(promptCopyButton).Should().BeNull();
                    ToolTip.GetTip(pathCopyButton).Should().BeNull();
                    ToolTip.GetTip(pathText).Should().BeNull();
                    Point? promptPosition = prompt.TranslatePoint(
                        new Point(0d, 0d),
                        window);
                    promptPosition.Should().NotBeNull();
                    Point? promptRight = prompt.TranslatePoint(
                        new Point(prompt.Bounds.Width, 0d),
                        window);
                    Point? promptCopyRight = promptCopyButton.TranslatePoint(
                        new Point(promptCopyButton.Bounds.Width, 0d),
                        window);
                    promptRight.Should().NotBeNull();
                    promptCopyRight.Should().NotBeNull();
                    double promptCopyRightInset =
                        promptRight.Value.X - promptCopyRight.Value.X;
                    promptCopyRightInset.Should().BeLessThan(15d);
                    Point? pathBottom = path.TranslatePoint(
                        new Point(0d, path.Bounds.Height),
                        window);
                    Point? repeatTop = repeat.TranslatePoint(
                        new Point(0d, 0d),
                        window);
                    pathBottom.Should().NotBeNull();
                    repeatTop.Should().NotBeNull();
                    double pathToRepeatGap = repeatTop.Value.Y - pathBottom.Value.Y;
                    pathToRepeatGap.Should().BeLessThan(20d);

                    window.MouseMove(
                        promptPosition.Value + new Vector(100d, 50d),
                        RawInputModifiers.None);
                    window.CaptureRenderedFrame();

                    promptCopyButton.IsHitTestVisible.Should().BeTrue();
                    pathCopyButton.IsHitTestVisible.Should().BeFalse();
                    Point? previewPosition = preview.TranslatePoint(
                        new Point(0d, 0d),
                        window);
                    previewPosition.Should().NotBeNull();
                    Point pointerPosition = previewPosition.Value + new Vector(62d, 62d);

                    window.MouseMove(pointerPosition, RawInputModifiers.None);
                    window.KeyPress(
                        Key.LeftShift,
                        RawInputModifiers.Shift,
                        PhysicalKey.ShiftLeft,
                        null);
                    window.CaptureRenderedFrame();

                    previewHost.Width.Should().BeGreaterThan(124d);
                    previewHost.Height.Should().BeGreaterThan(124d);
                    preview.Parent.Should().BeSameAs(content);
                    parameters.Parent.Should().BeSameAs(content);
                    prompt.Parent.Should().BeSameAs(content);
                    preview.ZIndex.Should().Be(1001);
                    preview.ZIndex.Should().BeGreaterThan(parameters.ZIndex);
                    preview.ZIndex.Should().BeGreaterThan(prompt.ZIndex);
                    Point? expandedPreviewPosition = previewHost.TranslatePoint(
                        new Point(0d, 0d),
                        content);
                    Point? promptContentPosition = prompt.TranslatePoint(
                        new Point(0d, 0d),
                        content);
                    expandedPreviewPosition.Should().NotBeNull();
                    promptContentPosition.Should().NotBeNull();
                    Rect expandedPreviewBounds = new(
                        expandedPreviewPosition.Value,
                        new Size(previewHost.Width, previewHost.Height));
                    Rect promptBounds = new(promptContentPosition.Value, prompt.Bounds.Size);
                    expandedPreviewBounds.Intersects(promptBounds).Should().BeTrue();
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
    public async Task CloseRequest_WhenViewIsShown_AnimatesBeforeExecutingAction()
    {
        string imagePath = Path.Combine(
            Path.GetTempPath(),
            $"atomic-art-metadata-closing-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(
            imagePath,
            GalleryThumbnailTestImages.CreatePngBytes(440, 220));

        try
        {
            await DispatchAsync(async () =>
            {
                bool isClosed = false;
                IRelayCommand closeCommand = new RelayCommand(() => isClosed = true);
                GenerationMetadataOverlayView view = CreateView(imagePath, closeCommand);
                Window window = Show(view, 750d, 780d);

                try
                {
                    await Task.Delay(400);
                    window.CaptureRenderedFrame();
                    Border panel = view.FindControl<Border>("PanelRoot")
                        ?? throw new InvalidOperationException("Metadata panel was not found.");
                    GenerationPreviewControl preview = view
                        .GetVisualDescendants()
                        .OfType<GenerationPreviewControl>()
                        .Single();
                    GenerationMetadataViewModel viewModel =
                        view.DataContext as GenerationMetadataViewModel
                        ?? throw new InvalidOperationException("Metadata view model was not found.");

                    viewModel.RequestCloseCommand.Execute(null);

                    isClosed.Should().BeFalse();
                    AssertPreviewOpacityChainIsOpaque(preview, view);

                    await Task.Delay(45);
                    window.CaptureRenderedFrame();

                    panel.Opacity.Should().BeLessThan(1d);
                    AssertPreviewOpacityChainIsOpaque(preview, view);
                    isClosed.Should().BeFalse();

                    await Task.Delay(70);

                    isClosed.Should().BeTrue();
                    AssertPreviewOpacityChainIsOpaque(preview, view);
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

    private static GenerationMetadataOverlayView CreateView(
        string imagePath,
        IRelayCommand? closeCommand = null)
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
            closeCommand ?? new RelayCommand(() => { }),
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

    private static void AssertPreviewOpacityChainIsOpaque(
        GenerationPreviewControl preview,
        GenerationMetadataOverlayView view)
    {
        preview.Opacity.Should().Be(1d);
        Image previewImage = preview.FindControl<Image>("PreviewImage")
            ?? throw new InvalidOperationException("Metadata preview image was not found.");
        previewImage.Opacity.Should().Be(1d);
        Border panel = view.FindControl<Border>("PanelRoot")
            ?? throw new InvalidOperationException("Metadata panel was not found.");
        preview
            .GetVisualAncestors()
            .TakeWhile(visual => !ReferenceEquals(visual, panel))
            .OfType<Control>()
            .Select(control => control.Opacity)
            .Should()
            .OnlyContain(opacity => opacity == 1d);
    }
}
