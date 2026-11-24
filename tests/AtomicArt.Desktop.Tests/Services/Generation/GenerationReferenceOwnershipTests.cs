using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationReferenceOwnershipTests
{
    [Fact]
    public async Task PruneAsync_AfterRestart_KeepsPanelImagesUntilTheLastOwnerIsRemoved()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceOwnershipTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            PanelAttachmentStore panels = GenerationReferenceOwnershipTests.CreatePanelStore(paths, references);
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            PanelAttachmentState first = panels.CreateState(image);
            PanelAttachmentState second = panels.CreateState(image);
            await panels.SaveAsync("first-panel", first, image, CancellationToken.None);
            await panels.SaveAsync("second-panel", second, image, CancellationToken.None);
            string? sharedPath = await panels.GetExistingFilePathAsync("first-panel", first, CancellationToken.None);
            GenerationReferenceStore restarted = GenerationReferenceStoreTests.CreateStore(root);

            await restarted.PruneAsync(new HashSet<string>(), CancellationToken.None);
            await panels.DeleteAsync("first-panel", first, CancellationToken.None);
            await restarted.PruneAsync(new HashSet<string>(), CancellationToken.None);

            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle().Which.Should().Be(sharedPath);
            (await panels.LoadAsync("second-panel", second, CancellationToken.None)).Should().BeEquivalentTo(image);
            Directory.GetFiles(paths.StateAttachmentsDirectory, "*.ref", SearchOption.AllDirectories).Should().ContainSingle();

            await panels.DeleteAsync("second-panel", second, CancellationToken.None);
            await restarted.PruneAsync(new HashSet<string>(), CancellationToken.None);

            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task LoadAsync_WithLegacyPanelImage_MovesItsContentIntoSharedStorage()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceOwnershipTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            PanelAttachmentStore panels = GenerationReferenceOwnershipTests.CreatePanelStore(paths, references);
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            PanelAttachmentState state = panels.CreateState(image);
            string directory = Path.Combine(paths.StateAttachmentsDirectory, new StatePathKeyEncoder().Encode("panel"));
            Directory.CreateDirectory(directory);
            string legacyPath = Path.Combine(directory, state.InternalFileName);
            await File.WriteAllBytesAsync(legacyPath, image.Content);

            AttachedImageDto? restored = await panels.LoadAsync("panel", state, CancellationToken.None);
            await references.PruneAsync(new HashSet<string>(), CancellationToken.None);

            restored.Should().BeEquivalentTo(image);
            File.Exists(legacyPath).Should().BeFalse();
            File.Exists(string.Concat(legacyPath, GenerationReferenceFilePolicy.OwnerFileExtension)).Should().BeTrue();
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();
            (await panels.LoadAsync("panel", state, CancellationToken.None)).Should().BeEquivalentTo(image);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task PruneAsync_WithCorruptOwner_KeepsTheStoredImages()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceOwnershipTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            await references.SaveOwnedAsync(Path.Combine("panel", "image.ref"), image, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(paths.StateAttachmentsDirectory, "panel", "image.ref"), "broken");

            await references.PruneAsync(new HashSet<string>(), CancellationToken.None);

            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task SaveOwnedAsync_WhilePruning_KeepsOneSharedFileForAllOwners()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceOwnershipTests));

        try
        {
            GenerationReferenceStore references = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            const int OwnerCount = 8;
            Task[] writes = Enumerable.Range(0, OwnerCount).Select(index => references.SaveOwnedAsync(
                Path.Combine($"panel-{index}", "image.ref"), image, CancellationToken.None)).ToArray();

            await Task.WhenAll(writes.Append(references.PruneAsync(new HashSet<string>(), CancellationToken.None)));
            await references.PruneAsync(new HashSet<string>(), CancellationToken.None);

            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();
            Directory.GetFiles(new AtomicArtDataPathProvider(root).StateAttachmentsDirectory, "*.ref",
                SearchOption.AllDirectories).Should().HaveCount(OwnerCount);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    private static PanelAttachmentStore CreatePanelStore(AtomicArtDataPathProvider paths, IGenerationReferenceStore references)
    {
        return new PanelAttachmentStore(paths, new StatePathKeyEncoder(), GenerationImageFormatRegistryTestFactory.Create(),
            new DataRootAccessCoordinator(), TestApiConfiguration.CreateTrustedFileStreamFactory(),
            NullLogger<PanelAttachmentStore>.Instance, references);
    }
}
