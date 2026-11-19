using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
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
    public async Task SaveAsync_WithCompletedItem_WritesRelativePathsToGalleryJson()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            string imagePath = Path.Combine(pathProvider.ArtDirectory, "generation.png");
            string thumbnailPath = Path.Combine(
                pathProvider.ThumbnailsDirectory,
                "generation.png");
            Directory.CreateDirectory(pathProvider.ArtDirectory);
            Directory.CreateDirectory(pathProvider.ThumbnailsDirectory);
            await File.WriteAllBytesAsync(imagePath, [0x01, 0x02, 0x03]);
            await File.WriteAllBytesAsync(thumbnailPath, [0x01, 0x02, 0x03]);
            GalleryStateService service = CreateService(
                CreateRealStore(pathProvider),
                new ExistingFileTrustedImageFileService(),
                pathProvider);

            await service.SaveAsync(
                [CreateGeneratedItem(imagePath, thumbnailPath)],
                CancellationToken.None);

            string statePath = Path.Combine(pathProvider.StateDirectory, new GalleryStateSection().FileName);
            File.Exists(statePath).Should().BeTrue();
            Directory.GetFiles(pathProvider.ArtDirectory, "*.json").Should().BeEmpty();
            string json = await File.ReadAllTextAsync(statePath);
            json.Should().Contain("\"imagePath\": \"Art/generation.png\"");
            json.Should().Contain("\"thumbnailPath\": \"Thumbnails/generation.png\"");
            json.Should().NotContain(imagePath.Replace("\\", "\\\\"));
            json.Should().NotContain(thumbnailPath.Replace("\\", "\\\\"));
            json.Should().NotContain("imageContent");
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithSharedReferences_KeepsFilesUntilTheLastEntryIsRemoved()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));
        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            IAppStateStore stateStore = CreateRealStore(paths);
            GalleryStateService service = CreateService(
                stateStore, new PassthroughTrustedImageFileService(), paths, references);
            AttachedImageDto image = new(
                "reference.png", GenerationImageContentTypes.Png, GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            Guid runId = Guid.NewGuid();
            IReadOnlyList<GenerationReference> saved = await references.SaveAsync(
                runId, new AttachedImageDto[] { image }, CancellationToken.None);
            GalleryItemState first = GalleryItemStateTestFactory.CreateGenerated(references: saved);
            GalleryItemState second = GalleryItemStateTestFactory.CreateGenerated(references: saved);
            await service.SaveAsync(new GalleryItemState[] { first, second }, CancellationToken.None);
            references.Release(runId);

            GalleryStateService reopened = CreateService(
                stateStore, new PassthroughTrustedImageFileService(), paths, references);
            GalleryState restored = await reopened.LoadAsync(CancellationToken.None);
            await reopened.SaveAsync(new GalleryItemState[] { second }, CancellationToken.None);

            restored.Items.Should().HaveCount(2).And.OnlyContain(item => item.References![0] == saved[0]);
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();

            await reopened.SaveAsync(Array.Empty<GalleryItemState>(), CancellationToken.None);

            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task LoadAsync_WithExistingMetadata_RecreatesGalleryItems()
    {
        AtomicArtDataPathProvider pathProvider = CreatePathProvider();
        string imagePath = Path.Combine(pathProvider.ArtDirectory, "generation.png");
        GalleryState state = await LoadStateAsync(
            CreateGeneratedItem("Art/generation.png"),
            new PassthroughTrustedImageFileService(),
            pathProvider);

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
        item.FailureCode.Should().Be(GenerationClientFailureCodes.Unknown);
    }

    [Fact]
    public async Task SaveAsync_WithRunningItem_SavesPlaceholderMetadata()
    {
        RecordingAppStateStore stateStore = new(typeof(GalleryState));
        GalleryStateService service = CreateService(
            stateStore,
            new PassthroughTrustedImageFileService());

        await service.SaveAsync(
            [CreateRunningItem()],
            CancellationToken.None);

        GalleryState savedState = stateStore.GetSavedStates<GalleryState>().Should().ContainSingle().Subject;
        savedState.Items.Should().ContainSingle();
        GalleryItemState item = savedState.Items[0];
        item.Status.Should().Be(GenerationItemStatus.Generating);
        item.CorrelationId.Should().Be(CorrelationId);
        item.GenerationOrdinal.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WithCompletedItem_CachesAbsoluteRuntimePath()
    {
        AtomicArtDataPathProvider pathProvider = CreatePathProvider();
        string imagePath = Path.Combine(pathProvider.ArtDirectory, "generation.png");
        RecordingAppStateStore stateStore = new(typeof(GalleryState));
        GalleryStateService service = CreateService(
            stateStore,
            new PassthroughTrustedImageFileService(),
            pathProvider);

        await service.SaveAsync(
            [CreateGeneratedItem(imagePath)],
            CancellationToken.None);
        GalleryState loadedState = await service.LoadAsync(CancellationToken.None);

        GalleryState savedState = stateStore.GetSavedStates<GalleryState>().Should().ContainSingle().Subject;
        savedState.Items.Should().ContainSingle();
        savedState.Items[0].ImagePath.Should().Be("Art/generation.png");
        loadedState.Items.Should().ContainSingle();
        loadedState.Items[0].ImagePath.Should().Be(imagePath);
    }

    private static GalleryStateService CreateService(
        IAppStateStore stateStore,
        ITrustedImageFileService trustedImageFileService,
        AtomicArtDataPathProvider? pathProvider = null,
        IGenerationReferenceStore? references = null)
    {
        AtomicArtDataPathProvider resolvedPathProvider = pathProvider ?? CreatePathProvider();
        GalleryStatePathConverter pathConverter = new(
            resolvedPathProvider,
            trustedImageFileService,
            NullLogger<GalleryStatePathConverter>.Instance);

        return new GalleryStateService(
            stateStore,
            references ?? Mock.Of<IGenerationReferenceStore>(),
            new DataRootAccessCoordinator(),
            pathConverter,
            new GalleryStateSection(),
            NullLogger<GalleryStateService>.Instance);
    }

    private static GalleryStateService CreateLoadService(
        GalleryItemState item,
        ITrustedImageFileService trustedImageFileService,
        AtomicArtDataPathProvider? pathProvider = null)
    {
        GalleryState state = new()
        {
            Items = [item]
        };

        return CreateService(
            new StubAppStateStore(state),
            trustedImageFileService,
            pathProvider);
    }

    private static async Task<GalleryState> LoadStateAsync(
        GalleryItemState item,
        ITrustedImageFileService trustedImageFileService,
        AtomicArtDataPathProvider? pathProvider = null)
    {
        GalleryStateService service = CreateLoadService(
            item,
            trustedImageFileService,
            pathProvider);

        return await service.LoadAsync(CancellationToken.None);
    }

    private static Task<GalleryState> LoadStateAsync(GalleryItemState item)
    {
        return LoadStateAsync(item, new PassthroughTrustedImageFileService());
    }

    private static AtomicArtDataPathProvider CreatePathProvider()
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(GalleryStateServiceTests));

        return new AtomicArtDataPathProvider(rootDirectory);
    }

    private static GalleryItemState GetOnlyItem(GalleryState state)
    {
        state.Items.Should().ContainSingle();

        return state.Items[0];
    }

    private static IAppStateStore CreateRealStore(AtomicArtDataPathProvider pathProvider)
    {
        return new AppStateStore(
            pathProvider,
            new DataRootAccessCoordinator(),
            TestApiConfiguration.CreateTrustedFileStreamFactory(),
            NullLogger<AppStateStore>.Instance);

    }

    private static GalleryItemState CreateGeneratedItem(
        string? imagePath,
        string? thumbnailPath = null)
    {
        return GalleryItemStateTestFactory.CreateGenerated(
            prompt: "Prompt",
            id: GeneratedItemId,
            createdAtUtc: CreatedAtUtc,
            imagePath: imagePath,
            thumbnailPath: thumbnailPath,
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
