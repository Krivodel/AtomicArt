using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
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
    public void Background_WhenShown_UsesPopupGradientWithoutBlur()
    {
        Dispatch(() =>
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
                    Border backgroundBase = panel
                        .GetVisualDescendants()
                        .OfType<Border>()
                        .Single(control => control.Classes.Contains("opaque-background-base"));
                    bool gradientFound = panel.TryFindResource(
                        "PopupGradientBrush",
                        out object? popupGradient);
                    ISolidColorBrush backgroundBrush = backgroundBase.Background
                        .Should()
                        .BeAssignableTo<ISolidColorBrush>()
                        .Subject;

                    gradientFound.Should().BeTrue();
                    panel.Background.Should().BeSameAs(popupGradient);
                    panel.BlurRadius.Should().Be(0d);
                    backgroundBrush.Color.A.Should().Be(byte.MaxValue);
                    backgroundBrush.Opacity.Should().Be(1d);
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

    [Fact]
    public void PromptText_WhenScrolled_UsesFullViewportWithMetadataInsets()
    {
        Dispatch(() =>
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
    public void PromptAndPathText_WhenShown_UseVerticalInsetsAndScrollEdgeFade()
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
                    string[] textBoxNames = ["PromptText", "PathText"];

                    foreach (string textBoxName in textBoxNames)
                    {
                        TextBox textBox = view.FindControl<TextBox>(textBoxName)
                            ?? throw new InvalidOperationException(
                                $"Text box '{textBoxName}' was not found.");
                        ScrollContentPresenter scrollPresenter = textBox
                            .GetVisualDescendants()
                            .OfType<ScrollContentPresenter>()
                            .Single();
                        textBox.TryFindResource(
                                "MetadataTextScrollInsets",
                                out object? paddingResource)
                            .Should()
                            .BeTrue();
                        Thickness fadeInsets = paddingResource
                            .Should()
                            .BeOfType<Thickness>()
                            .Subject;

                        fadeInsets.Left.Should().Be(0d);
                        fadeInsets.Right.Should().Be(0d);
                        TextBoxScrollContentAssertions
                            .GetTextPresenter(textBox)
                            .Margin
                            .Should()
                            .Be(fadeInsets);
                        VerticalFadeMaskBehavior.GetInsets(scrollPresenter)
                            .Should()
                            .Be(fadeInsets);
                        scrollPresenter.OpacityMask.Should()
                            .BeOfType<LinearGradientBrush>();
                    }
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
    public void Preview_WhenShown_AttachesLoadingSessionAndBindsImagePath()
    {
        Dispatch(() =>
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
    public void Preview_WhenClosed_DisposesLoadingSession()
    {
        Dispatch(() =>
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
            TestLocalizationTextProvider.Default);
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
            TestLocalizationTextProvider.Default);
        GenerationMetadataOverlayView view = previewSessionFactory is null
            ? new GenerationMetadataOverlayView()
            : new GenerationMetadataOverlayView(previewSessionFactory);
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
