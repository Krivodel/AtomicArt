using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryStateServiceTests
{
    private static readonly Guid GeneratedItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RunningItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CorrelationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedAtUtc = new(2026, 7, 6, 9, 0, 5, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAsync_WithCompletedItem_WritesGalleryJsonOutsideArt()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            string imagePath = Path.Combine(pathProvider.ArtDirectory, "generation.png");
            Directory.CreateDirectory(pathProvider.ArtDirectory);
            await File.WriteAllBytesAsync(imagePath, [0x01, 0x02, 0x03]);
            IStateWriteScheduler scheduler = CreateRealScheduler(pathProvider);
            GalleryStateService service = CreateService(
                new GalleryState(),
                scheduler,
                new ExistingFileTrustedImageFileService());

            await service.SaveAsync(
                [CreateGeneratedItem(imagePath)],
                CancellationToken.None);
            await scheduler.FlushAsync(CancellationToken.None);

            string statePath = Path.Combine(pathProvider.StateDirectory, new GalleryStateSection().FileName);
            File.Exists(statePath).Should().BeTrue();
            Directory.GetFiles(pathProvider.ArtDirectory, "*.json").Should().BeEmpty();
            string json = await File.ReadAllTextAsync(statePath);
            json.Should().Contain(imagePath.Replace("\\", "\\\\"));
            json.Should().NotContain("imageContent");
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithExistingMetadata_RecreatesGalleryItems()
    {
        string imagePath = Path.Combine("D:", "AtomicArt", "Art", "generation.png");
        GalleryState state = await LoadStateAsync(CreateGeneratedItem(imagePath));

        GalleryItemState item = GetOnlyItem(state);
        item.Id.Should().Be(GeneratedItemId);
        item.Status.Should().Be(GenerationItemStatus.Generated);
        item.ImagePath.Should().Be(imagePath);
        item.Prompt.Should().Be("Prompt");
    }

    [Fact]
    public async Task LoadAsync_WithMissingImage_KeepsItemWithoutImagePath()
    {
        GalleryState state = await LoadStateAsync(
            CreateGeneratedItem("missing.png"),
            new RejectingTrustedImageFileService());

        GalleryItemState item = GetOnlyItem(state);
        item.Status.Should().Be(GenerationItemStatus.Generated);
        item.ImagePath.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithRunningItems_MarksAsFailed()
    {
        GalleryState state = await LoadStateAsync(CreateRunningItem());

        GalleryItemState item = GetOnlyItem(state);
        item.Status.Should().Be(GenerationItemStatus.Failed);
        item.CorrelationId.Should().BeNull();
        item.GenerationOrdinal.Should().BeNull();
        item.ImagePath.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithRunningItem_SchedulesPlaceholderMetadata()
    {
        RecordingStateWriteScheduler scheduler = new();
        GalleryStateService service = CreateService(
            new GalleryState(),
            scheduler,
            new PassthroughTrustedImageFileService());

        await service.SaveAsync(
            [CreateRunningItem()],
            CancellationToken.None);

        GalleryState savedState = scheduler.SavedState.Should()
            .BeOfType<GalleryState>()
            .Subject;
        savedState.Items.Should().ContainSingle();
        GalleryItemState item = savedState.Items[0];
        item.Status.Should().Be(GenerationItemStatus.Generating);
        item.CorrelationId.Should().Be(CorrelationId);
        item.GenerationOrdinal.Should().Be(0);
    }

    private static GalleryStateService CreateService(
        GalleryState initialState,
        IStateWriteScheduler scheduler,
        ITrustedImageFileService trustedImageFileService)
    {
        return new GalleryStateService(
            new StubAppStateStore(initialState),
            scheduler,
            trustedImageFileService,
            new GalleryStateSection(),
            NullLogger<GalleryStateService>.Instance);
    }

    private static GalleryStateService CreateLoadService(
        GalleryItemState item,
        ITrustedImageFileService trustedImageFileService)
    {
        GalleryState state = new()
        {
            Items = [item]
        };

        return CreateService(
            state,
            new RecordingStateWriteScheduler(),
            trustedImageFileService);
    }

    private static async Task<GalleryState> LoadStateAsync(
        GalleryItemState item,
        ITrustedImageFileService trustedImageFileService)
    {
        GalleryStateService service = CreateLoadService(item, trustedImageFileService);

        return await service.LoadAsync(CancellationToken.None);
    }

    private static Task<GalleryState> LoadStateAsync(GalleryItemState item)
    {
        return LoadStateAsync(item, new PassthroughTrustedImageFileService());
    }

    private static GalleryItemState GetOnlyItem(GalleryState state)
    {
        state.Items.Should().ContainSingle();

        return state.Items[0];
    }

    private static IStateWriteScheduler CreateRealScheduler(AtomicArtDataPathProvider pathProvider)
    {
        AppStateStore stateStore = new(
            pathProvider,
            NullLogger<AppStateStore>.Instance);

        return new StateWriteScheduler(
            stateStore,
            NullLogger<StateWriteScheduler>.Instance);
    }

    private static GalleryItemState CreateGeneratedItem(string? imagePath)
    {
        return GalleryItemStateTestFactory.CreateGenerated(
            prompt: "Prompt",
            id: GeneratedItemId,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath,
            completedAtUtc: CompletedAtUtc,
            generationDuration: TimeSpan.FromSeconds(5),
            price: new GenerationPriceDto(0.05m, "USD", "actual"),
            usage: new GenerationUsageDto(120, 340),
            attachedImagesCount: 1);
    }

    private static GalleryItemState CreateRunningItem()
    {
        return new GalleryItemState
        {
            Id = RunningItemId,
            ModelId = ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ModelDisplayName = ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            Prompt = "Running prompt",
            AspectRatio = GenerationAspectRatios.Auto,
            Resolution = TestGenerationOutputMetadata.GeneratedImageResolution,
            CreatedAtUtc = CreatedAtUtc,
            Status = GenerationItemStatus.Generating,
            AttachedImagesCount = 2,
            CorrelationId = CorrelationId,
            GenerationOrdinal = 0
        };
    }

}
