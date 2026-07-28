using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Tests.Common.Generation;

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

    [Fact]
    public void RebaseDataRootPaths_WithManagedPaths_UpdatesExistingItems()
    {
        string sourceRootDirectory = Path.GetFullPath(Path.Combine("Root", "Source"));
        string destinationRootDirectory = Path.GetFullPath(Path.Combine("Root", "Destination"));
        string imagePath = Path.Combine(sourceRootDirectory, "Art", "generation.png");
        string thumbnailPath = Path.Combine(
            sourceRootDirectory,
            "Thumbnails",
            "generation.png");
        (
            GalleryItemsController controller,
            GenerationItemViewModel item) = RestoreSingleItem(
                new PassthroughTrustedImageFileService(),
                imagePath,
                thumbnailPath);

        controller.RebaseDataRootPaths(
            sourceRootDirectory,
            destinationRootDirectory);

        item.ImagePath.Should().Be(Path.Combine(
            destinationRootDirectory,
            "Art",
            "generation.png"));
        item.ThumbnailPath.Should().Be(Path.Combine(
            destinationRootDirectory,
            "Thumbnails",
            "generation.png"));
    }

    [Fact]
    public void CreatePlaceholders_WithExistingItem_AssignsNewerTimestampsInDisplayOrder()
    {
        GalleryItemsController controller = CreateController(
            new PassthroughTrustedImageFileService());
        GalleryItemState existingState = GalleryItemStateTestFactory.CreateGenerated(
            id: ItemId,
            createdAtUtc: CreatedAtUtc,
            galleryOrderTimestampUtc: CreatedAtUtc);
        controller.RestoreItems([existingState]);
        GenerationLifecycleEvent startedEvent =
            GalleryLifecycleTestFactory.CreateStartedEvent(
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                CreatedAtUtc,
                generationCount: 2,
                attachedImagesCount: 0);

        IReadOnlyList<GenerationItemViewModel> placeholders =
            controller.CreatePlaceholders(startedEvent);

        placeholders.Should().HaveCount(2);

        DateTime firstTimestampUtc = placeholders[0].GalleryOrderTimestampUtc
            ?? throw new InvalidOperationException(
                "The first placeholder order timestamp is required.");
        DateTime secondTimestampUtc = placeholders[1].GalleryOrderTimestampUtc
            ?? throw new InvalidOperationException(
                "The second placeholder order timestamp is required.");
        DateTime existingTimestampUtc = existingState.GalleryOrderTimestampUtc
            ?? throw new InvalidOperationException(
                "The existing item order timestamp is required.");
        firstTimestampUtc.Should().BeAfter(secondTimestampUtc);
        secondTimestampUtc.Should().BeAfter(existingTimestampUtc);
    }

    [Fact]
    public void CreateGeneratedItems_WithMultipleItems_AssignsTimestampsInDisplayOrder()
    {
        GalleryItemsController controller = CreateController(
            new PassthroughTrustedImageFileService());
        GenerationItemDto olderItem = GenerationItemDtoTestFactory.Create(
            id: Guid.Parse("88888888-8888-8888-8888-888888888888"),
            createdAtUtc: CreatedAtUtc);
        GenerationItemDto newerItem = GenerationItemDtoTestFactory.Create(
            id: Guid.Parse("99999999-9999-9999-9999-999999999999"),
            createdAtUtc: CreatedAtUtc.AddMinutes(1));

        IReadOnlyList<GenerationItemViewModel> generatedItems =
            controller.CreateGeneratedItems([olderItem, newerItem], 0);

        generatedItems.Select(item => item.Id).Should()
            .Equal(newerItem.Id, olderItem.Id);
        generatedItems.Select(item => item.GalleryOrderTimestampUtc).Should()
            .BeInDescendingOrder();
    }

    [Fact]
    public void UpdateFromResult_WithDifferentCreationTime_PreservesGalleryOrderTimestamp()
    {
        GalleryItemsController controller = CreateController(
            new PassthroughTrustedImageFileService());
        GenerationLifecycleEvent startedEvent =
            GalleryLifecycleTestFactory.CreateStartedEvent(
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                CreatedAtUtc,
                generationCount: 1,
                attachedImagesCount: 0);
        GenerationItemViewModel placeholder =
            controller.CreatePlaceholders(startedEvent).Single();
        DateTime? galleryOrderTimestampUtc =
            placeholder.GalleryOrderTimestampUtc;
        GenerationItemDto result =
            GenerationItemDtoTestFactory.Create(
                id: placeholder.Id,
                createdAtUtc: CreatedAtUtc.AddMinutes(1));

        placeholder.UpdateFromResult(result, null, null);

        placeholder.GalleryOrderTimestampUtc.Should()
            .Be(galleryOrderTimestampUtc);
    }

    private static GalleryItemsController CreateController(ITrustedImageFileService trustedImageFileService)
    {
        return new GalleryItemsController(
            trustedImageFileService,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestApiConfiguration.CreateGalleryOrderTimestampPolicy());
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
