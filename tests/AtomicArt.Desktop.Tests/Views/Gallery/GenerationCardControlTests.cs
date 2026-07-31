using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.Input;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationCardControlTests : DesktopControlTestBase
{
    private const string Prompt = "Prompt";
    private const string AspectRatio = GenerationAspectRatios.Auto;

    private static readonly Guid ItemId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Size PreviewSize = new(220d, 220d);
    private static readonly Rect DefaultViewportBounds = new(0d, 0d, 1000d, 600d);

    [Theory]
    [InlineData(KeyModifiers.None, false)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control, true)]
    [InlineData(KeyModifiers.Alt, true)]
    [InlineData(KeyModifiers.Meta, false)]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control, true)]
    public void HasExpansionModifier_WithKeyModifiers_DetectsSupportedModifier(
        KeyModifiers modifiers,
        bool expectedResult)
    {
        bool result = GenerationPreviewExpansionController.HasExpansionModifier(modifiers);

        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(KeyModifiers.None, false)]
    [InlineData(KeyModifiers.Meta, false)]
    [InlineData(KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Control, true)]
    [InlineData(KeyModifiers.Alt, true)]
    public void ResolveFileRevealCommand_WithModifiers_SelectsExpectedCommand(
        KeyModifiers modifiers,
        bool expectsNewWindowCommand)
    {
        RelayCommand defaultCommand = new(() => { });
        RelayCommand newWindowCommand = new(() => { });

        IRelayCommand? result = GenerationCardControl.ResolveFileRevealCommand(
            modifiers,
            defaultCommand,
            newWindowCommand);

        result.Should().BeSameAs(
            expectsNewWindowCommand ? newWindowCommand : defaultCommand);
    }

    [Fact]
    public void Calculate_WithWideSource_ScalesFullAspectRatioAndFitsRightViewportEdge()
    {
        AssertExpansion(
            new Size(440d, 220d),
            new Rect(780d, 40d, 220d, 220d),
            new Size(748d, 374d),
            new Vector(-528d, -40d));
    }

    [Fact]
    public void Calculate_WithTallSource_ScalesFullAspectRatioAndFitsViewportStart()
    {
        AssertExpansion(
            new Size(220d, 440d),
            new Rect(40d, 380d, 220d, 220d),
            new Size(374d, 748d),
            new Vector(-40d, -380d));
    }

    [Fact]
    public void Calculate_WithCenteredWideSource_ExpandsEvenlyAroundPreview()
    {
        AssertExpansion(
            new Size(330d, 220d),
            new Rect(390d, 40d, 220d, 220d),
            new Size(561d, 374d),
            new Vector(-170.5d, -40d));
    }

    [Fact]
    public void Calculate_WithOffsetViewport_FitsExpandedPreviewInsideActualVisibleBounds()
    {
        Size sourceSize = new(440d, 220d);
        Rect previewBounds = new(780d, 40d, 220d, 220d);
        Rect viewportBounds = new(20d, 0d, 960d, 600d);

        (Size size, Vector translation) = Calculate(
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(748d, 374d));
        translation.Should().Be(new Vector(-548d, -40d));
        (previewBounds.Left + translation.X + size.Width).Should().Be(viewportBounds.Right);
    }

    [Fact]
    public void GetImageDragPathOrDefault_WithExistingFullImageAndThumbnail_ReturnsFullImagePath()
    {
        using ExistingImagePaths paths = new();

        string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(paths.Item);

        dragPath.Should().Be(paths.ImagePath);
    }

    [Fact]
    public void GetImageDragPathOrDefault_WithMissingFullImageAndExistingThumbnail_ReturnsNull()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "atomic-art-missing-generation-card-drag-test.png");
        string thumbnailPath = Path.GetTempFileName();

        try
        {
            File.Delete(imagePath);
            GenerationItemViewModel item = CreateItem(imagePath, thumbnailPath);

            string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(item);

            dragPath.Should().BeNull();
        }
        finally
        {
            File.Delete(thumbnailPath);
        }
    }

    [Fact]
    public void GetImageDragPreviewPathOrDefault_WithExistingThumbnail_ReturnsThumbnailPath()
    {
        using ExistingImagePaths paths = new();

        string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(paths.Item);

        previewPath.Should().Be(paths.ThumbnailPath);
    }

    [Fact]
    public void GetImageDragPreviewPathOrDefault_WithMissingThumbnail_ReturnsFullImagePath()
    {
        string imagePath = Path.GetTempFileName();
        string thumbnailPath = Path.Combine(Path.GetTempPath(), "atomic-art-missing-generation-card-preview-test.png");

        try
        {
            File.Delete(thumbnailPath);
            GenerationItemViewModel item = CreateItem(imagePath, thumbnailPath);

            string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(item);

            previewPath.Should().Be(imagePath);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Theory]
    [InlineData(
        "Сделай первую картинку (игра 3 в ряд, 2д) в стиле второй " +
        "картинки (игра borderlands 3). Не меняй игру на 1 картинке, " +
        "нужны лишь другие текстуры/рисовка/фон.")]
    [InlineData(
        "Первая строка\n" +
        "Вторая строка\n" +
        "Третья строка\n" +
        "Скрытая четвёртая строка")]
    [InlineData(
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт" +
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт" +
        "ОченьДлинныйНеразрывныйПромптОченьДлинныйНеразрывныйПромпт")]
    public async Task Prompt_WhenReusedCardTextOverflows_EndsWithEllipsisAsync(
        string overflowingPrompt)
    {
        await DispatchAsync(() =>
        {
            const string Ellipsis = "…";
            GenerationItemViewModel initialItem = CreateItem(
                "initial-image.png",
                "initial-thumbnail.jpg");
            GenerationItemViewModel overflowingItem = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg",
                overflowingPrompt);
            GenerationCardControl control = new()
            {
                DataContext = initialItem
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                TextBlock initialPrompt = GetPromptTextBlock(control, Prompt);
                GetRenderedText(initialPrompt).Should().Be(Prompt);

                control.DataContext = overflowingItem;
                Size cardSize = new(
                    GalleryLayoutService.CardWidth,
                    GalleryLayoutService.CardHeight);
                control.Measure(cardSize);
                control.Arrange(new Rect(cardSize));

                OverflowEllipsisTextBlock prompt = GetPromptTextBlock(
                        control,
                        overflowingPrompt)
                    .Should()
                    .BeOfType<OverflowEllipsisTextBlock>()
                    .Subject;
                prompt.TextTrimming.Should().NotBe(TextTrimming.None);
                prompt.TextLayout.TextLines.Should().HaveCount(prompt.MaxLines);
                string renderedText = GetRenderedText(prompt);
                renderedText.Should().EndWith(Ellipsis);
                renderedText.Should().NotEndWith(Ellipsis + Ellipsis);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Prompt_WhenCardShown_MatchesStandardTextAppearanceAsync()
    {
        await DispatchAsync(() =>
        {
            GenerationItemViewModel item = CreateItem(
                "missing-image.png",
                "missing-thumbnail.jpg");
            GenerationCardControl control = new()
            {
                DataContext = item
            };
            Window window = Show(
                control,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight);

            try
            {
                TextBlock prompt = GetPromptTextBlock(control, Prompt);
                TextBlock model = GetPromptTextBlock(
                    control,
                    item.ModelDisplayName);

                prompt.FontFamily.Should().Be(model.FontFamily);
                prompt.FontSize.Should().Be(model.FontSize);
                prompt.FontStretch.Should().Be(model.FontStretch);
                prompt.FontStyle.Should().Be(model.FontStyle);
                prompt.Foreground.Should().Be(model.Foreground);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static TextBlock GetPromptTextBlock(
        GenerationCardControl control,
        string prompt)
    {
        return control
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(textBlock => string.Equals(
                textBlock.Text,
                prompt,
                StringComparison.Ordinal));
    }

    private static string GetRenderedText(TextBlock textBlock)
    {
        return string.Concat(
            textBlock.TextLayout.TextLines
                .SelectMany(line => line.TextRuns)
                .Select(run => run.Text.ToString()));
    }

    private static GenerationItemViewModel CreateItem(
        string imagePath,
        string thumbnailPath,
        string prompt = Prompt)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            prompt: prompt,
            aspectRatio: AspectRatio,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath);
        GenerationItemViewModel viewModel = new(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default)
        {
            ThumbnailPath = thumbnailPath
        };

        return viewModel;
    }

    private static (Size Size, Vector Translation) Calculate(
        Size sourceSize,
        Rect previewBounds,
        Rect? viewportBounds = null)
    {
        return GenerationPreviewExpansionCalculator.Calculate(
            PreviewSize,
            sourceSize,
            previewBounds,
            viewportBounds ?? DefaultViewportBounds);
    }

    private static void AssertExpansion(
        Size sourceSize,
        Rect previewBounds,
        Size expectedSize,
        Vector expectedTranslation)
    {
        (Size size, Vector translation) = Calculate(sourceSize, previewBounds);

        size.Should().Be(expectedSize);
        translation.Should().Be(expectedTranslation);
    }

    private sealed class ExistingImagePaths : IDisposable
    {
        public GenerationItemViewModel Item { get; }
        public string ImagePath { get; }
        public string ThumbnailPath { get; }

        public ExistingImagePaths()
        {
            ImagePath = Path.GetTempFileName();
            ThumbnailPath = Path.GetTempFileName();
            Item = CreateItem(ImagePath, ThumbnailPath);
        }

        public void Dispose()
        {
            File.Delete(ImagePath);
            File.Delete(ThumbnailPath);
        }
    }
}
