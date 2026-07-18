using FluentAssertions;
using Xunit;

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
        (
            GalleryItemsController controller,
            GenerationItemViewModel item) = RestoreSingleItem(
                new PassthroughTrustedImageFileService(),
                "generation.png",
                null);

        item.Id.Should().Be(ItemId);
        item.IsGenerated.Should().BeTrue();
        item.ImagePath.Should().Be("generation.png");
        item.Prompt.Should().Be("Prompt");
        controller.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void RestoreItems_WithMissingImage_RecreatesItemWithoutImagePath()
    {
        (
            GalleryItemsController _,
            GenerationItemViewModel item) = RestoreSingleItem(
                new RejectingTrustedImageFileService(),
                "missing.png",
                null);

        item.IsGenerated.Should().BeTrue();
        item.ImagePath.Should().BeNull();
        item.HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void RestoreItems_WithSavedThumbnail_RecreatesItemWithThumbnailPath()
    {
        (
            GalleryItemsController _,
            GenerationItemViewModel item) = RestoreSingleItem(
                new PassthroughTrustedImageFileService(),
                "generation.png",
                "thumbnail.png");

        item.ImagePath.Should().Be("generation.png");
        item.ThumbnailPath.Should().Be("thumbnail.png");
        item.DisplayThumbnailPath.Should().Be("thumbnail.png");
    }

    [Fact]
    public void RestoreItems_WithUntrustedThumbnail_DropsThumbnailPath()
    {
        (
            GalleryItemsController _,
            GenerationItemViewModel item) = RestoreSingleItem(
                new RejectingThumbnailTrustedImageFileService(),
                "generation.png",
                "thumbnail.png");

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
        return GalleryItemStateTestFactory.CreateGenerated(
            prompt: "Prompt",
            id: ItemId,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath,
            thumbnailPath: thumbnailPath);
    }

    private static GenerationItemViewModel RestoreSingleItem(
        GalleryItemsController controller,
        GalleryItemState state)
    {
        controller.RestoreItems([state]);

        controller.Items.Should().ContainSingle();

        return controller.Items[0];
    }

    private static (GalleryItemsController Controller, GenerationItemViewModel Item)
        RestoreSingleItem(
            ITrustedImageFileService trustedImageFileService,
            string? imagePath,
            string? thumbnailPath)
    {
        GalleryItemsController controller = CreateController(trustedImageFileService);
        GalleryItemState state = CreateState(imagePath, thumbnailPath);
        GenerationItemViewModel item = RestoreSingleItem(controller, state);

        return (controller, item);
    }

    private sealed class RejectingThumbnailTrustedImageFileService : TrustedImageFileServiceTestDouble
    {
        public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
        {
            if (string.Equals(path, "thumbnail.png", StringComparison.Ordinal))
            {
                return null;
            }

            return path;
        }

        public override void DeleteTrustedImageFileIfExists(
            string? path,
            string modelId,
            Action<string> validateResolvedPath)
        {
            throw new NotSupportedException("Deletion is not used by this test.");
        }
    }
}
