using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed partial class GalleryStateServiceTests
{
    [Fact]
    public async Task SaveAsync_WithSharedGalleryImage_KeepsOneFileUntilTheLastCardAndChatAreRemoved()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("image.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            Guid lease = Guid.NewGuid();
            GenerationReference reference = (await references.SaveAsync(lease,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            string path = references.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a stored image.");
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            TrustedImageFileService trusted = GalleryStateServiceTests.CreateSharedImageFileService(paths);
            GenerationChatHistory editedHistory = new(new GenerationChatMessage[]
                { new GenerationChatMessage(true, "Image removed from the message", CompletedAtUtc, Array.Empty<GenerationReference>()) }, true);
            GalleryItemState original = GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                imagePath: path, chatHistory: editedHistory, chatResult: new GenerationChatResult("Original", reference));
            GalleryItemState branch = GalleryItemStateTestFactory.CreateGenerated(id: RunningItemId, imagePath: path,
                chatHistory: new GenerationChatHistory(new GenerationChatMessage[]
                    { new GenerationChatMessage(true, "Shared image", CompletedAtUtc, new GenerationReference[] { reference }) }),
                chatResult: new GenerationChatResult("Branch", reference));
            GalleryStateService service = GalleryStateServiceTests.CreateService(state, trusted, paths, references);
            await service.SaveAsync(new GalleryItemState[] { original, branch }, CancellationToken.None);
            path = references.ResolveExistingFilePath(reference) ?? throw new InvalidOperationException("Expected a gallery image.");
            references.Release(lease);
            GalleryItemDeletionService deletion = new(trusted, new GenerationImageFileNamePolicy(),
                new DataRootAccessCoordinator(), NullLogger<GalleryItemDeletionService>.Instance);

            await deletion.DeleteFilesAsync(new GalleryItemDeletionRequest(GeneratedItemId, original.ModelId, path, null),
                CancellationToken.None);
            await service.SaveAsync(new GalleryItemState[] { original }, CancellationToken.None);
            GalleryState restored = await GalleryStateServiceTests.CreateService(state, trusted, paths, references)
                .LoadAsync(CancellationToken.None);

            restored.Items.Should().ContainSingle().Which.ImagePath.Should().Be(path);
            (await references.LoadAsync(reference, CancellationToken.None)).Should().BeEquivalentTo(image);
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
            Directory.GetFiles(paths.ArtDirectory).Should().ContainSingle();

            await service.SaveAsync(Array.Empty<GalleryItemState>(), CancellationToken.None);

            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WithExistingDuplicate_RemovesItOnlyAfterGalleryCommit(bool failCommit)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("image.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            Guid lease = Guid.NewGuid();
            GenerationReference reference = (await references.SaveAsync(lease,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            Directory.CreateDirectory(paths.ArtDirectory);
            string originalPath = Path.Combine(paths.ArtDirectory,
                new GenerationImageFileNamePolicy().BuildFileName(GeneratedItemId, GeneratedItemId, ".png"));
            await File.WriteAllBytesAsync(originalPath, image.Content);
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            await state.SaveAsync(new GalleryStateSection(), new GalleryState
            {
                Items = new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                    imagePath: originalPath, chatResult: new GenerationChatResult("Stored result", reference)) }
            }, CancellationToken.None);
            references.Release(lease);
            Mock<IAppStateStore> failing = new();
            failing.Setup(store => store.LoadAsync<GalleryState>(It.IsAny<IStateSection>(), It.IsAny<CancellationToken>()))
                .Returns((IStateSection section, CancellationToken ct) => state.LoadAsync<GalleryState>(section, ct));
            failing.Setup(store => store.SaveAsync(It.IsAny<IStateSection>(), It.IsAny<GalleryState>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Simulated gallery commit failure."));
            GalleryStateService service = GalleryStateServiceTests.CreateService(failCommit ? failing.Object : state,
                GalleryStateServiceTests.CreateSharedImageFileService(paths), paths, references);

            if (failCommit)
            {
                Func<Task> load = () => service.LoadAsync(CancellationToken.None);
                await load.Should().ThrowAsync<IOException>();
                File.Exists(originalPath).Should().BeTrue();
            }
            else
            {
                GalleryState restored = await service.LoadAsync(CancellationToken.None);
                restored.Items.Single().ImagePath.Should().Be(references.ResolveExistingFilePath(reference));
                File.Exists(originalPath).Should().BeFalse();
                GalleryState reopened = await GalleryStateServiceTests.CreateService(state,
                    GalleryStateServiceTests.CreateSharedImageFileService(paths), paths, references).LoadAsync(CancellationToken.None);
                reopened.Items.Single().ImagePath.Should().Be(restored.Items.Single().ImagePath);
            }

            (await references.LoadAsync(reference, CancellationToken.None)).Should().BeEquivalentTo(image);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WithDifferentOrCorruptImage_KeepsTheGalleryOriginal(bool corruptSharedImage)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("image.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            Guid lease = Guid.NewGuid();
            GenerationReference reference = (await references.SaveAsync(lease,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            Directory.CreateDirectory(paths.ArtDirectory);
            string path = Path.Combine(paths.ArtDirectory,
                new GenerationImageFileNamePolicy().BuildFileName(GeneratedItemId, GeneratedItemId, ".png"));
            await File.WriteAllBytesAsync(path, corruptSharedImage ? image.Content : GalleryThumbnailTestImages.CreatePngBytes(80, 120));

            if (corruptSharedImage)
            {
                await File.WriteAllBytesAsync(references.ResolveExistingFilePath(reference)
                    ?? throw new InvalidOperationException("Expected an image."), new byte[] { 0 });
            }

            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            await state.SaveAsync(new GalleryStateSection(), new GalleryState
            {
                Items = new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                    imagePath: path, chatResult: new GenerationChatResult("Stored result", reference)) }
            }, CancellationToken.None);
            GalleryStateService service = GalleryStateServiceTests.CreateService(state,
                GalleryStateServiceTests.CreateSharedImageFileService(paths), paths, references);

            GalleryState restored = await service.LoadAsync(CancellationToken.None);

            restored.Items.Single().ImagePath.Should().Be(path);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenDeletingTheLastResultFails_KeepsTheCommittedImageInArt()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            Guid lease = Guid.NewGuid();
            GenerationReference reference = (await references.SaveAsync(lease, new AttachedImageDto[]
            {
                new AttachedImageDto("image.png", GenerationImageContentTypes.Png,
                    GalleryThumbnailTestImages.CreatePngBytes(120, 80))
            }, CancellationToken.None)).Single();
            string originalPath = references.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a stored image.");
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            TrustedImageFileService trusted = GalleryStateServiceTests.CreateSharedImageFileService(paths);
            GalleryItemState item = GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                imagePath: originalPath, chatResult: new GenerationChatResult("Result", reference));
            await GalleryStateServiceTests.CreateService(state, trusted, paths, references)
                .SaveAsync(new GalleryItemState[] { item }, CancellationToken.None);
            references.Release(lease);
            string artPath = references.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a gallery image.");
            Mock<IAppStateStore> failing = new();
            failing.Setup(store => store.SaveAsync(It.IsAny<IStateSection>(), It.IsAny<GalleryState>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Simulated gallery commit failure."));
            GalleryStateService service = GalleryStateServiceTests.CreateService(failing.Object, trusted, paths, references);

            Func<Task> save = () => service.SaveAsync(Array.Empty<GalleryItemState>(), CancellationToken.None);

            await save.Should().ThrowAsync<IOException>();
            File.Exists(artPath).Should().BeTrue();
            Path.GetDirectoryName(artPath).Should().Be(paths.ArtDirectory);
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
            GalleryState restored = await GalleryStateServiceTests.CreateService(state, trusted, paths, references)
                .LoadAsync(CancellationToken.None);
            restored.Items.Should().ContainSingle().Which.ImagePath.Should().Be(artPath);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WithAStoredHistoryPathAfterPromotion_RestoresTheCardAndPersistsItsArtPath(bool hasChatResult)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            Guid lease = Guid.NewGuid();
            GenerationReference reference = (await references.SaveAsync(lease, new AttachedImageDto[]
            {
                new AttachedImageDto("image.png", GenerationImageContentTypes.Png,
                    GalleryThumbnailTestImages.CreatePngBytes(120, 80))
            }, CancellationToken.None)).Single();
            string historyPath = references.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a stored image.");
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            await state.SaveAsync(new GalleryStateSection(), new GalleryState
            {
                Items = new GalleryItemState[] { GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                    imagePath: Path.GetRelativePath(root, historyPath),
                    chatResult: hasChatResult ? new GenerationChatResult("Result", reference) : null) }
            }, CancellationToken.None);
            await references.PromoteAsync(lease, new GenerationReference[] { reference }, CancellationToken.None);
            references.Release(lease);
            string artPath = references.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a gallery image.");
            TrustedImageFileService trusted = GalleryStateServiceTests.CreateSharedImageFileService(paths);
            GalleryStatePathConverter converter = new(paths, trusted, NullLogger<GalleryStatePathConverter>.Instance);
            GenerationReferenceStore restarted = GenerationReferenceStoreTests.CreateStore(root);

            converter.IsStoredImageFileMissing(Path.GetRelativePath(root, historyPath)).Should().BeFalse();
            GalleryState restored = await GalleryStateServiceTests.CreateService(state, trusted, paths, restarted)
                .LoadAsync(CancellationToken.None);
            GalleryState persisted = await state.LoadAsync<GalleryState>(new GalleryStateSection(), CancellationToken.None);

            restored.Items.Should().ContainSingle().Which.ImagePath.Should().Be(artPath);
            persisted.Items.Should().ContainSingle().Which.ImagePath.Should().Be($"Art/{Path.GetFileName(artPath)}");
            Directory.GetFiles(paths.ArtDirectory).Should().ContainSingle();
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task SaveAsync_AfterRemovingTheOriginalCard_MovesOnlyItsHistoricalImageOutOfArt()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto originalImage = new("original.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            AttachedImageDto branchImage = new("branch.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(80, 120));
            Guid lease = Guid.NewGuid();
            IReadOnlyList<GenerationReference> images = await references.SaveAsync(lease,
                new AttachedImageDto[] { originalImage, branchImage }, CancellationToken.None);
            GenerationChatImageCall originalCall = new(images[0].Hash, "Exact parent prompt", false, "parent-call");
            GenerationChatImageCall branchCall = new(images[1].Hash, "Exact branch prompt\n  Keep this spacing.", false, "branch-call");
            GenerationChatHistory originalHistory = new(new GenerationChatMessage[]
            {
                new(false, "Parent request", CreatedAtUtc, Array.Empty<GenerationReference>()),
                new(true, "Original", CreatedAtUtc, new GenerationReference[] { images[0] }, ImageGeneration: originalCall)
                { SourceCardId = GeneratedItemId }
            }, includesCardTurn: true);
            GalleryItemState original = GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId,
                imagePath: references.ResolveExistingFilePath(images[0]), chatHistory: originalHistory,
                chatResult: new GenerationChatResult("Original", images[0]) { ImageGeneration = originalCall });
            GalleryItemState branch = GalleryItemStateTestFactory.CreateGenerated(id: RunningItemId,
                imagePath: references.ResolveExistingFilePath(images[1]), chatHistory: new GenerationChatHistory(new GenerationChatMessage[]
                {
                    originalHistory.Messages[0],
                    originalHistory.Messages[1],
                    new(false, "Branch request", CompletedAtUtc, Array.Empty<GenerationReference>()),
                    new(true, "Branch", CompletedAtUtc, new GenerationReference[] { images[1] }, ImageGeneration: branchCall)
                    { SourceCardId = RunningItemId }
                }, includesCardTurn: true), chatResult: new GenerationChatResult("Branch", images[1]) { ImageGeneration = branchCall });
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            TrustedImageFileService trusted = GalleryStateServiceTests.CreateSharedImageFileService(paths);
            GalleryStateService service = GalleryStateServiceTests.CreateService(state, trusted, paths, references);
            await service.SaveAsync(new GalleryItemState[] { original, branch }, CancellationToken.None);
            references.Release(lease);

            await service.SaveAsync(new GalleryItemState[] { branch }, CancellationToken.None);

            File.Exists(Path.Combine(paths.ChatsDirectory, $"{originalHistory.Id:N}.json")).Should().BeFalse();
            string? originalPath = references.ResolveExistingFilePath(images[0]);
            string? branchPath = references.ResolveExistingFilePath(images[1]);
            Path.GetDirectoryName(originalPath).Should().Be(GenerationReferenceStoreTests.GetHistoryDirectory(root));
            Path.GetDirectoryName(branchPath).Should().Be(paths.ArtDirectory);
            Directory.GetFiles(paths.ArtDirectory).Should().ContainSingle();
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();
            (await references.LoadAsync(images[0], CancellationToken.None)).Should().BeEquivalentTo(originalImage);

            GenerationReferenceStore restarted = GenerationReferenceStoreTests.CreateStore(root);
            GalleryState restored = await GalleryStateServiceTests.CreateService(state, trusted, paths, restarted)
                .LoadAsync(CancellationToken.None);

            restored.Items.Should().ContainSingle().Which.ImagePath.Should().Be(branchPath);
            (await restarted.LoadAsync(images[0], CancellationToken.None)).Should().BeEquivalentTo(originalImage);
            using GenerationChatStore chats = new(state, NullLogger<GenerationChatStore>.Instance, paths, new DataRootAccessCoordinator());
            Guid branchChatId = restored.Items.Single().ChatId ?? throw new InvalidOperationException("The branch chat identifier is missing.");
            GenerationChatHistory history = await chats.LoadAsync(branchChatId, CancellationToken.None);
            history.Messages[1].ImageGeneration.Should().Be(originalCall);
            history.Messages[3].ImageGeneration.Should().Be(branchCall);
            history.Messages[1].SourceCardId.Should().Be(GeneratedItemId);
            history.Messages[3].SourceCardId.Should().Be(RunningItemId);

            using (LocalCodexInputPreparation input = new(restarted, GenerationImageFormatRegistryTestFactory.Create(),
                TestLocalizationTextProvider.Default))
            {
                ImageGenerationRequestDto request = new(LocalCodexImageModel.Id, "Give the exact prompts", GenerationAspectRatios.Auto,
                    LocalCodexImageModel.Automatic, LocalCodexImageModel.Option.Temperature.Default, 1, Array.Empty<AttachedImageDto>());
                await input.PrepareAsync(request, history, CancellationToken.None);
                JsonElement wire = JsonSerializer.SerializeToElement(input.Inputs);
                List<string?> prompts = [];

                foreach (JsonElement entry in wire.EnumerateArray().Where(entry => entry.GetProperty("type").GetString() == "text"))
                {
                    using JsonDocument message = JsonDocument.Parse(entry.GetProperty("text").GetString()
                        ?? throw new InvalidOperationException("The history input is missing."));

                    if (message.RootElement.TryGetProperty("imageGeneration", out JsonElement call))
                    {
                        prompts.Add(call.GetProperty("revisedPrompt").GetString());
                    }
                }

                prompts.Should().Equal(originalCall.RevisedPrompt, branchCall.RevisedPrompt);
            }

            await service.SaveAsync(new GalleryItemState[] { original, branch }, CancellationToken.None);

            Path.GetDirectoryName(references.ResolveExistingFilePath(images[0])).Should().Be(paths.ArtDirectory);
            Directory.GetFiles(paths.ArtDirectory).Should().HaveCount(2);
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    private static TrustedImageFileService CreateSharedImageFileService(AtomicArtDataPathProvider paths)
    {
        return new TrustedImageFileService(paths, GenerationImageFormatRegistryTestFactory.Create(),
            NullLogger<TrustedImageFileService>.Instance, TestApiConfiguration.CreateGenerationOptionsWrapper(),
            TestApiConfiguration.CreateTrustedFileStreamFactory());
    }
}
