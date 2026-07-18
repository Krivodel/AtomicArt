using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

public sealed class GalleryItemsControllerTests
{
    private static readonly Guid ItemId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 6, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RestoreItems_WithSavedState_RecreatesGalleryItems()
    {
        GalleryItemsController controller = CreateController(new PassthroughTrustedImageFileService());
        GalleryItemState state = CreateState("generation.png", null);

        controller.RestoreItems([state]);

        controller.Items.Should().ContainSingle();
        GenerationItemViewModel item = controller.Items[0];
        item.Id.Should().Be(ItemId);
        item.IsGenerated.Should().BeTrue();
        item.ImagePath.Should().Be("generation.png");
        item.Prompt.Should().Be("Prompt");
        controller.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void RestoreItems_WithMissingImage_RecreatesItemWithoutImagePath()
    {
        GalleryItemsController controller = CreateController(new RejectingTrustedImageFileService());
        GalleryItemState state = CreateState("missing.png", null);

        controller.RestoreItems([state]);

        controller.Items.Should().ContainSingle();
        GenerationItemViewModel item = controller.Items[0];
        item.IsGenerated.Should().BeTrue();
        item.ImagePath.Should().BeNull();
        item.HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void RestoreItems_WithSavedThumbnail_RecreatesItemWithThumbnailPath()
    {
        GalleryItemsController controller = CreateController(new PassthroughTrustedImageFileService());
        GalleryItemState state = CreateState("generation.png", "thumbnail.png");

        controller.RestoreItems([state]);

        controller.Items.Should().ContainSingle();
        GenerationItemViewModel item = controller.Items[0];
        item.ImagePath.Should().Be("generation.png");
        item.ThumbnailPath.Should().Be("thumbnail.png");
        item.DisplayThumbnailPath.Should().Be("thumbnail.png");
    }

    [Fact]
    public void RestoreItems_WithUntrustedThumbnail_DropsThumbnailPath()
    {
        GalleryItemsController controller = CreateController(new RejectingThumbnailTrustedImageFileService());
        GalleryItemState state = CreateState("generation.png", "thumbnail.png");

        controller.RestoreItems([state]);

        controller.Items.Should().ContainSingle();
        GenerationItemViewModel item = controller.Items[0];
        item.ImagePath.Should().Be("generation.png");
        item.ThumbnailPath.Should().BeNull();
        item.DisplayThumbnailPath.Should().Be("generation.png");
    }

    private static GalleryItemsController CreateController(ITrustedImageFileService trustedImageFileService)
    {
        return new GalleryItemsController(
            trustedImageFileService,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }

    private static GalleryItemState CreateState(string? imagePath, string? thumbnailPath)
    {
        return new GalleryItemState
        {
            Id = ItemId,
            ModelId = ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ModelDisplayName = ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            Prompt = "Prompt",
            AspectRatio = GenerationAspectRatios.Auto,
            Resolution = TestGenerationOutputMetadata.GeneratedImageResolution,
            CreatedAtUtc = CreatedAtUtc,
            Status = GenerationItemStatus.Generated,
            ImagePath = imagePath,
            ThumbnailPath = thumbnailPath,
            AttachedImagesCount = 0
        };
    }

    private sealed class RejectingThumbnailTrustedImageFileService : ITrustedImageFileService
    {
        public string? GetTrustedImagePathOrDefault(string? path, string modelId)
        {
            if (string.Equals(path, "thumbnail.png", StringComparison.Ordinal))
            {
                return null;
            }

            return path;
        }

        public string GetTrustedImagePath(string? path, string modelId)
        {
            return GetTrustedImagePathOrDefault(path, modelId)
                ?? throw new InvalidOperationException("Image path is not trusted.");
        }

        public void DeleteTrustedImageFileIfExists(
            string? path,
            string modelId,
            Action<string> validateResolvedPath)
        {
            throw new NotSupportedException("Deletion is not used by this test.");
        }
    }
}
