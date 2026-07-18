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
    private const string NanoBanana2ModelId = "nano-banana-2";
    private const string NanoBanana2DisplayName = "Nano Banana 2";
    private const string Prompt = "Prompt";
    private const string AspectRatio = "Авто";
    private const string Resolution = "1024x1024";

    private static readonly Guid ItemId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0d, 0d, false)]
    [InlineData(3d, 0d, false)]
    [InlineData(4d, 0d, true)]
    [InlineData(3d, 3d, true)]
    public void HasReachedDragStartThreshold_WithPointerMovement_DetectsActualDrag(
        double x,
        double y,
        bool expectedResult)
    {
        bool result = GenerationCardControl.HasReachedDragStartThreshold(
            new Point(0d, 0d),
            new Point(x, y));

        result.Should().Be(expectedResult);
    }

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
    public void Calculate_WithWideSource_PreservesShortSideAndFitsRightViewportEdge()
    {
        Size previewSize = new(220d, 220d);
        Size sourceSize = new(440d, 220d);
        Rect previewBounds = new(780d, 40d, 220d, 220d);
        Rect viewportBounds = new(0d, 0d, 1000d, 600d);

        (Size size, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
            previewSize,
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(440d, 220d));
        translation.Should().Be(new Vector(-220d, 0d));
    }

    [Fact]
    public void Calculate_WithTallSource_PreservesShortSideAndFitsBottomViewportEdge()
    {
        Size previewSize = new(220d, 220d);
        Size sourceSize = new(220d, 440d);
        Rect previewBounds = new(40d, 380d, 220d, 220d);
        Rect viewportBounds = new(0d, 0d, 1000d, 600d);

        (Size size, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
            previewSize,
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(220d, 440d));
        translation.Should().Be(new Vector(0d, -220d));
    }

    [Fact]
    public void Calculate_WithCenteredWideSource_ExpandsEvenlyAroundPreview()
    {
        Size previewSize = new(220d, 220d);
        Size sourceSize = new(330d, 220d);
        Rect previewBounds = new(390d, 40d, 220d, 220d);
        Rect viewportBounds = new(0d, 0d, 1000d, 600d);

        (Size size, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
            previewSize,
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(330d, 220d));
        translation.Should().Be(new Vector(-55d, 0d));
    }

    [Fact]
    public void Calculate_WithOffsetViewport_FitsExpandedPreviewInsideActualVisibleBounds()
    {
        Size previewSize = new(220d, 220d);
        Size sourceSize = new(440d, 220d);
        Rect previewBounds = new(780d, 40d, 220d, 220d);
        Rect viewportBounds = new(20d, 0d, 960d, 600d);

        (Size size, Vector translation) = GenerationPreviewExpansionCalculator.Calculate(
            previewSize,
            sourceSize,
            previewBounds,
            viewportBounds);

        size.Should().Be(new Size(440d, 220d));
        translation.Should().Be(new Vector(-240d, 0d));
        (previewBounds.Left + translation.X + size.Width).Should().Be(viewportBounds.Right);
    }

    [Fact]
    public void GetImageDragPathOrDefault_WithExistingFullImageAndThumbnail_ReturnsFullImagePath()
    {
        string imagePath = Path.GetTempFileName();
        string thumbnailPath = Path.GetTempFileName();

        try
        {
            GenerationItemViewModel item = CreateItem(imagePath);
            item.ThumbnailPath = thumbnailPath;

            string? dragPath = GenerationCardControl.GetImageDragPathOrDefault(item);

            dragPath.Should().Be(imagePath);
        }
        finally
        {
            File.Delete(imagePath);
            File.Delete(thumbnailPath);
        }
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
        string imagePath = Path.GetTempFileName();
        string thumbnailPath = Path.GetTempFileName();

        try
        {
            GenerationItemViewModel item = CreateItem(imagePath);
            item.ThumbnailPath = thumbnailPath;

            string? previewPath = GenerationCardControl.GetImageDragPreviewPathOrDefault(item);

            previewPath.Should().Be(thumbnailPath);
        }
        finally
        {
            File.Delete(imagePath);
            File.Delete(thumbnailPath);
        }
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
        GenerationItemDto item = new(
            ItemId,
            NanoBanana2ModelId,
            NanoBanana2DisplayName,
            Prompt,
            AspectRatio,
            Resolution,
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            imagePath);

        return new GenerationItemViewModel(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }
}
