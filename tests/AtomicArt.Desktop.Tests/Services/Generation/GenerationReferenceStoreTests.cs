using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed partial class GenerationReferenceStoreTests
{
    [Fact]
    public async Task SaveAsync_WithIdenticalImages_KeepsOneFileAndEachDisplayNameAcrossRestart()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));
        try
        {
            GenerationReferenceStore store = CreateStore(root);
            byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(800, 400);
            AttachedImageDto first = new("first.png", GenerationImageContentTypes.Png, bytes);
            AttachedImageDto second = new("second.png", GenerationImageContentTypes.Png, bytes);
            Guid runId = Guid.NewGuid();

            IReadOnlyList<GenerationReference> references = await store.SaveAsync(
                runId, new AttachedImageDto[] { first, second }, CancellationToken.None);
            store.Release(runId);
            AttachedImageDto? reopened = await CreateStore(root).LoadAsync(references[1], CancellationToken.None);
            string? dragPath = await store.GetExistingFilePathAsync(references[1], CancellationToken.None);

            references.Select(reference => reference.FileName).Should().Equal(first.FileName, second.FileName);
            references[0].Hash.Should().Be(references[1].Hash);
            Directory.GetFiles(GetHistoryDirectory(root)).Should().ContainSingle().Which.Should().Be(dragPath);
            reopened.Should().BeEquivalentTo(second);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public async Task PruneAsync_WithAnUncommittedRun_KeepsItsImageUntilReleased()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));
        try
        {
            GenerationReferenceStore store = CreateStore(root);
            AttachedImageDto image = new(
                "reference.png", GenerationImageContentTypes.Png, GalleryThumbnailTestImages.CreatePngBytes(800, 400));
            Guid runId = Guid.NewGuid();
            IReadOnlyList<GenerationReference> references = await store.SaveAsync(
                runId, new AttachedImageDto[] { image }, CancellationToken.None);
            HashSet<string> retained = [];

            await store.PruneAsync(retained, CancellationToken.None);
            Directory.GetFiles(GetHistoryDirectory(root)).Should().ContainSingle();

            store.Release(runId);
            retained.Add(references[0].Hash);
            await store.PruneAsync(retained, CancellationToken.None);
            Directory.GetFiles(GetHistoryDirectory(root)).Should().ContainSingle();

            retained.Clear();
            await store.PruneAsync(retained, CancellationToken.None);
            Directory.GetFiles(GetHistoryDirectory(root)).Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    internal static GenerationReferenceStore CreateStore(string root)
    {
        return new GenerationReferenceStore(
            new AtomicArtDataPathProvider(root),
            new DataRootAccessCoordinator(),
            TestApiConfiguration.CreateTrustedFileStreamFactory(),
            GenerationImageFormatRegistryTestFactory.Create(),
            NullLogger<GenerationReferenceStore>.Instance);
    }

    internal static string GetHistoryDirectory(string root)
    {
        AtomicArtDataPathProvider paths = new(root);
        return Path.Combine(paths.StateAttachmentsDirectory, "History");
    }
}
