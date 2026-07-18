using Avalonia;
using Avalonia.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationCardControlTests
{
    private const string Prompt = "Prompt";
    private const string AspectRatio = "Авто";

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

    [Fact]
    public void Calculate_WithWideSource_ScalesFullAspectRatioAndFitsRightViewportEdge()
    {
        Size sourceSize = new(440d, 220d);
        Rect previewBounds = new(780d, 40d, 220d, 220d);

        (Size size, Vector translation) = Calculate(sourceSize, previewBounds);

        size.Should().Be(new Size(748d, 374d));
        translation.Should().Be(new Vector(-528d, -40d));
    }

    [Fact]
    public void Calculate_WithTallSource_ScalesFullAspectRatioAndFitsViewportStart()
    {
        Size sourceSize = new(220d, 440d);
        Rect previewBounds = new(40d, 380d, 220d, 220d);

        (Size size, Vector translation) = Calculate(sourceSize, previewBounds);

        size.Should().Be(new Size(374d, 748d));
        translation.Should().Be(new Vector(-40d, -380d));
    }

    [Fact]
    public void Calculate_WithCenteredWideSource_ExpandsEvenlyAroundPreview()
    {
        Size sourceSize = new(330d, 220d);
        Rect previewBounds = new(390d, 40d, 220d, 220d);

        (Size size, Vector translation) = Calculate(sourceSize, previewBounds);

        size.Should().Be(new Size(561d, 374d));
        translation.Should().Be(new Vector(-170.5d, -40d));
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
        GenerationItemViewModel item = CreateItem(paths.ImagePath);
        item.ThumbnailPath = paths.ThumbnailPath;

        string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(item);

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
            GenerationItemViewModel item = CreateItem(imagePath);
            item.ThumbnailPath = thumbnailPath;

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
        GenerationItemViewModel item = CreateItem(paths.ImagePath);
        item.ThumbnailPath = paths.ThumbnailPath;

        string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(item);

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
            GenerationItemViewModel item = CreateItem(imagePath);
            item.ThumbnailPath = thumbnailPath;

            string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(item);

            previewPath.Should().Be(imagePath);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static GenerationItemViewModel CreateItem(string imagePath)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            prompt: Prompt,
            aspectRatio: AspectRatio,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath);

        return new GenerationItemViewModel(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
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

    private sealed class ExistingImagePaths : IDisposable
    {
        public string ImagePath { get; }
        public string ThumbnailPath { get; }

        public ExistingImagePaths()
        {
            ImagePath = Path.GetTempFileName();
            ThumbnailPath = Path.GetTempFileName();
        }

        public void Dispose()
        {
            File.Delete(ImagePath);
            File.Delete(ThumbnailPath);
        }
    }
}
