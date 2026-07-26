using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryStateConsistencyServiceTests
{
    private static readonly Guid MissingImageItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FailedItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BatchId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ReconcileAsync_WithMissingGeneratedImage_RemovesItemAndDeletesThumbnail()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStateConsistencyServiceTests),
            nameof(ReconcileAsync_WithMissingGeneratedImage_RemovesItemAndDeletesThumbnail));

        try
        {
            AtomicArtDataPathProvider pathProvider = CreatePathProvider(rootDirectory);
            string fileName = BuildManagedFileName(MissingImageItemId);
            GalleryItemState missingImageItem = CreateItem(
                MissingImageItemId,
                GenerationItemStatus.Generated,
                Path.Combine("Art", fileName),
                Path.Combine("Thumbnails", fileName));
            GalleryItemState failedItem = CreateItem(
                FailedItemId,
                GenerationItemStatus.Failed,
                null,
                null);
            RecordingAppStateStore stateStore = new(
                new GalleryState
                {
                    Items = [missingImageItem, failedItem]
                });
            RecordingDeletionService deletionService = new();
            DataRootAccessCoordinator accessCoordinator = new();
            GalleryStateConsistencyService service = CreateService(
                stateStore,
                deletionService,
                accessCoordinator,
                CreatePathConverter(pathProvider));

            await service.ReconcileAsync(CancellationToken.None);

            stateStore.SaveCallCount.Should().Be(1);
            GalleryState savedState = stateStore.SavedState
                ?? throw new InvalidOperationException("Expected reconciled gallery state.");
            savedState.Items.Should().ContainSingle()
                .Which.Id.Should().Be(FailedItemId);
            GalleryItemDeletionRequest request = deletionService.Requests.Should()
                .ContainSingle()
                .Which;
            request.ItemId.Should().Be(MissingImageItemId);
            request.ImagePath.Should().Be(
                Path.Combine(pathProvider.ArtDirectory, fileName));
            request.ThumbnailPath.Should().Be(
                Path.Combine(pathProvider.ThumbnailsDirectory, fileName));
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task ReconcileAsync_WithExistingUntrustedImage_DoesNotChangeState()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStateConsistencyServiceTests),
            nameof(ReconcileAsync_WithExistingUntrustedImage_DoesNotChangeState));

        try
        {
            AtomicArtDataPathProvider pathProvider = CreatePathProvider(rootDirectory);
            string fileName = BuildManagedFileName(MissingImageItemId);
            string imagePath = Path.Combine(pathProvider.ArtDirectory, fileName);
            await File.WriteAllBytesAsync(imagePath, [0x01]);
            DateTime galleryOrderTimestampUtc = new(
                2026,
                7,
                26,
                12,
                0,
                0,
                DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(imagePath, galleryOrderTimestampUtc);

            if (OperatingSystem.IsWindows())
            {
                File.SetCreationTimeUtc(imagePath, galleryOrderTimestampUtc);
            }

            GalleryItemState generatedItem = CreateItem(
                MissingImageItemId,
                GenerationItemStatus.Generated,
                Path.Combine("Art", fileName),
                null,
                galleryOrderTimestampUtc);
            RecordingAppStateStore stateStore = new(
                new GalleryState
                {
                    Items = [generatedItem]
                });
            RecordingDeletionService deletionService = new();
            DataRootAccessCoordinator accessCoordinator = new();
            GalleryStateConsistencyService service = CreateService(
                stateStore,
                deletionService,
                accessCoordinator,
                CreatePathConverter(pathProvider));

            await service.ReconcileAsync(CancellationToken.None);

            stateStore.SaveCallCount.Should().Be(0);
            deletionService.Requests.Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task ReconcileAsync_AfterRestartWithMissingImage_UpdatesJsonAndDeletesThumbnail()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStateConsistencyServiceTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = CreatePathProvider(rootDirectory);
            GenerationImageFileNamePolicy fileNamePolicy = new();
            string fileName = fileNamePolicy.BuildFileName(
                BatchId,
                MissingImageItemId,
                ".png");
            string thumbnailPath = Path.Combine(
                pathProvider.ThumbnailsDirectory,
                fileName);
            Directory.CreateDirectory(pathProvider.ThumbnailsDirectory);
            await File.WriteAllBytesAsync(
                thumbnailPath,
                GenerationImageTestData.ValidPngBytes);
            DataRootAccessCoordinator accessCoordinator = new();
            AppStateStore stateStore = new(
                pathProvider,
                accessCoordinator,
                NullLogger<AppStateStore>.Instance);
            GalleryStateSection section = new();
            GalleryItemState missingImageItem = CreateItem(
                MissingImageItemId,
                GenerationItemStatus.Generated,
                Path.Combine("Art", fileName),
                Path.Combine("Thumbnails", fileName));
            GalleryState storedState = new()
            {
                Items = [missingImageItem]
            };
            await stateStore.SaveAsync(
                section,
                storedState,
                CancellationToken.None);
            TrustedImageFileService trustedImageFileService = new(
                pathProvider,
                GenerationImageFormatRegistryTestFactory.Create(),
                NullLogger<TrustedImageFileService>.Instance);
            GalleryStatePathConverter pathConverter = new(
                pathProvider,
                trustedImageFileService,
                NullLogger<GalleryStatePathConverter>.Instance);
            GalleryItemDeletionService deletionService = new(
                trustedImageFileService,
                fileNamePolicy,
                accessCoordinator,
                NullLogger<GalleryItemDeletionService>.Instance);
            GalleryStateConsistencyService service = CreateService(
                stateStore,
                deletionService,
                accessCoordinator,
                pathConverter,
                section);

            await service.ReconcileAsync(CancellationToken.None);

            GalleryState reconciledState = await stateStore.LoadAsync<GalleryState>(
                section,
                CancellationToken.None);
            reconciledState.Items.Should().BeEmpty();
            File.Exists(thumbnailPath).Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task ReconcileAsync_WithLegacyItems_PersistsAndAppliesGalleryOrder()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStateConsistencyServiceTests),
            nameof(ReconcileAsync_WithLegacyItems_PersistsAndAppliesGalleryOrder));

        try
        {
            AtomicArtDataPathProvider pathProvider = CreatePathProvider(rootDirectory);
            Guid topItemId =
                Guid.Parse("44444444-4444-4444-4444-444444444444");
            Guid bottomItemId =
                Guid.Parse("55555555-5555-5555-5555-555555555555");
            string topFileName = BuildManagedFileName(topItemId);
            string bottomFileName = BuildManagedFileName(bottomItemId);
            string topImagePath = Path.Combine(
                pathProvider.ArtDirectory,
                topFileName);
            string bottomImagePath = Path.Combine(
                pathProvider.ArtDirectory,
                bottomFileName);
            await File.WriteAllBytesAsync(
                topImagePath,
                GenerationImageTestData.ValidPngBytes);
            await File.WriteAllBytesAsync(
                bottomImagePath,
                GenerationImageTestData.ValidPngBytes);
            DateTime createdAtUtc = new(
                2026,
                7,
                26,
                12,
                0,
                0,
                DateTimeKind.Utc);
            GalleryItemState topItem = CreateItem(
                topItemId,
                GenerationItemStatus.Generated,
                Path.Combine("Art", topFileName),
                null,
                createdAtUtc: createdAtUtc);
            GalleryItemState bottomItem = CreateItem(
                bottomItemId,
                GenerationItemStatus.Generated,
                Path.Combine("Art", bottomFileName),
                null,
                createdAtUtc: createdAtUtc);
            RecordingAppStateStore stateStore = new(
                new GalleryState
                {
                    Items = [topItem, bottomItem]
                });
            RecordingDeletionService deletionService = new();
            DataRootAccessCoordinator accessCoordinator = new();
            GalleryStateConsistencyService service = CreateService(
                stateStore,
                deletionService,
                accessCoordinator,
                CreatePathConverter(pathProvider));

            await service.ReconcileAsync(CancellationToken.None);

            GalleryState savedState = stateStore.SavedState
                ?? throw new InvalidOperationException(
                    "Expected gallery order metadata to be saved.");
            DateTime topTimestampUtc =
                savedState.Items[0].GalleryOrderTimestampUtc
                ?? throw new InvalidOperationException(
                    "Expected a top item order timestamp.");
            DateTime bottomTimestampUtc =
                savedState.Items[1].GalleryOrderTimestampUtc
                ?? throw new InvalidOperationException(
                    "Expected a bottom item order timestamp.");
            topTimestampUtc.Should().BeAfter(bottomTimestampUtc);
            File.GetLastWriteTimeUtc(topImagePath).Should()
                .Be(topTimestampUtc);
            File.GetLastWriteTimeUtc(bottomImagePath).Should()
                .Be(bottomTimestampUtc);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static GalleryStateConsistencyService CreateService(
        IAppStateStore stateStore,
        IGalleryItemDeletionService deletionService,
        IDataRootAccessCoordinator accessCoordinator,
        GalleryStatePathConverter pathConverter,
        GalleryStateSection? section = null)
    {
        IGalleryFileOrderSynchronizer fileOrderSynchronizer =
            new GalleryFileOrderSynchronizer(
                accessCoordinator,
                pathConverter,
                new GenerationImageFileNamePolicy(),
                NullLogger<GalleryFileOrderSynchronizer>.Instance);

        return new GalleryStateConsistencyService(
            stateStore,
            deletionService,
            fileOrderSynchronizer,
            accessCoordinator,
            pathConverter,
            section ?? new GalleryStateSection(),
            NullLogger<GalleryStateConsistencyService>.Instance);
    }

    private static AtomicArtDataPathProvider CreatePathProvider(string rootDirectory)
    {
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Directory.CreateDirectory(pathProvider.ArtDirectory);
        Directory.CreateDirectory(pathProvider.ThumbnailsDirectory);

        return pathProvider;
    }

    private static GalleryStatePathConverter CreatePathConverter(
        AtomicArtDataPathProvider pathProvider)
    {
        return new GalleryStatePathConverter(
            pathProvider,
            new RejectingTrustedImageFileService(),
            NullLogger<GalleryStatePathConverter>.Instance);
    }

    private static string BuildManagedFileName(Guid itemId)
    {
        GenerationImageFileNamePolicy fileNamePolicy = new();

        return fileNamePolicy.BuildFileName(BatchId, itemId, ".png");
    }

    private static GalleryItemState CreateItem(
        Guid itemId,
        GenerationItemStatus status,
        string? imagePath,
        string? thumbnailPath,
        DateTime? galleryOrderTimestampUtc = null,
        DateTime? createdAtUtc = null)
    {
        GalleryItemState generatedItem = GalleryItemStateTestFactory.CreateGenerated(
            id: itemId,
            createdAtUtc: createdAtUtc,
            imagePath: imagePath,
            thumbnailPath: thumbnailPath);

        return new GalleryItemState
        {
            Id = generatedItem.Id,
            ModelId = generatedItem.ModelId,
            ModelDisplayName = generatedItem.ModelDisplayName,
            Prompt = generatedItem.Prompt,
            AspectRatio = generatedItem.AspectRatio,
            Resolution = generatedItem.Resolution,
            CreatedAtUtc = generatedItem.CreatedAtUtc,
            GalleryOrderTimestampUtc = galleryOrderTimestampUtc,
            Status = status,
            ImagePath = generatedItem.ImagePath,
            ThumbnailPath = generatedItem.ThumbnailPath,
            CompletedAtUtc = generatedItem.CompletedAtUtc,
            GenerationDuration = generatedItem.GenerationDuration,
            Price = generatedItem.Price,
            Usage = generatedItem.Usage,
            AttachedImagesCount = generatedItem.AttachedImagesCount
        };
    }

    private sealed class RecordingDeletionService : IGalleryItemDeletionService
    {
        public IReadOnlyList<GalleryItemDeletionRequest> Requests => _requests;

        private readonly List<GalleryItemDeletionRequest> _requests = [];

        public Task DeleteFilesAsync(
            GalleryItemDeletionRequest request,
            CancellationToken ct)
        {
            _requests.Add(request);

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAppStateStore : IAppStateStore
    {
        public int SaveCallCount { get; private set; }
        public GalleryState? SavedState { get; private set; }

        private readonly GalleryState _loadedState;

        public RecordingAppStateStore(GalleryState loadedState)
        {
            _loadedState = loadedState ?? throw new ArgumentNullException(nameof(loadedState));
        }

        public Task<TState> LoadAsync<TState>(
            IStateSection section,
            CancellationToken ct)
        {
            if (_loadedState is TState state)
            {
                return Task.FromResult(state);
            }

            throw new InvalidOperationException("Unexpected gallery state type.");
        }

        public Task SaveAsync<TState>(
            IStateSection section,
            TState state,
            CancellationToken ct)
            where TState : notnull
        {
            return SaveAsync(section, (object)state, ct);
        }

        public Task SaveAsync(
            IStateSection section,
            object state,
            CancellationToken ct)
        {
            if (state is not GalleryState galleryState)
            {
                throw new InvalidOperationException("Unexpected gallery state type.");
            }

            SaveCallCount++;
            SavedState = galleryState;

            return Task.CompletedTask;
        }
    }
}
