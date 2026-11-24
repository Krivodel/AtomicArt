using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed partial class GalleryStateServiceTests
{
    [Fact]
    public async Task SaveAsync_WithVariantGroup_KeepsItsFrozenContextUntilTheLastVariantIsRemoved()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GalleryStateServiceTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            IAppStateStore state = GalleryStateServiceTests.CreateRealStore(paths);
            using GenerationChatStore chats = new(state, NullLogger<GenerationChatStore>.Instance, paths, new DataRootAccessCoordinator());
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            Guid lease = Guid.NewGuid();
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(80, 120));
            GenerationReference reference = (await references.SaveAsync(lease, new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            GenerationChatHistory context = new(new GenerationChatMessage[]
            {
                new GenerationChatMessage(true, "Original answer", CreatedAtUtc, Array.Empty<GenerationReference>()),
                new GenerationChatMessage(false, "Frozen request", CompletedAtUtc, new GenerationReference[] { reference })
            });
            GenerationChatGroup group = new(CorrelationId, GeneratedItemId, context.Id, context.ReferenceHashes) { Context = context };
            GalleryItemState original = GalleryItemStateTestFactory.CreateGenerated(id: GeneratedItemId, imagePath: null);
            GalleryItemState first = GalleryItemStateTestFactory.CreateGenerated(id: RunningItemId, imagePath: null, chatGroup: group);
            GalleryItemState second = GalleryItemStateTestFactory.CreateGenerated(id: CorrelationId, imagePath: null,
                chatGroup: group with { VariantOrdinal = 1 });
            GalleryStateService service = GalleryStateServiceTests.CreateService(state, new PassthroughTrustedImageFileService(), paths,
                references: references, chats: chats);

            await service.SaveAsync(new GalleryItemState[] { original, first, second }, CancellationToken.None);
            references.Release(lease);
            GalleryState persisted = await state.LoadAsync<GalleryState>(new GalleryStateSection(), CancellationToken.None);
            GenerationChatGroup saved = persisted.Items.Single(item => item.Id == RunningItemId).ChatGroup
                ?? throw new InvalidOperationException("Expected a saved variant group.");
            saved.Context.Should().BeNull();
            saved.Id.Should().Be(group.Id);

            await service.SaveAsync(new GalleryItemState[] { second }, CancellationToken.None);
            GenerationChatHistory restored = await chats.LoadAsync(saved.ContextId, CancellationToken.None);

            restored.Messages.Select(message => message.Text).Should().Equal("Original answer", "Frozen request");
            Directory.GetFiles(paths.ChatsDirectory).Should().ContainSingle();
            (await references.LoadAsync(reference, CancellationToken.None)).Should().BeEquivalentTo(image);

            await service.SaveAsync(Array.Empty<GalleryItemState>(), CancellationToken.None);

            Directory.GetFiles(paths.ChatsDirectory).Should().BeEmpty();
            references.ResolveExistingFilePath(reference).Should().BeNull();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }
}
