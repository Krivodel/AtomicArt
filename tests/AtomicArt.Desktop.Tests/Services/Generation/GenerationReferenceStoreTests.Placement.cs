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
    private static readonly TimeSpan PlacementTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task SynchronizeAsync_WhenGalleryOwnershipChanges_MovesTheSameFileBetweenArtAndHistory()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = GenerationReferenceStoreTests.CreatePlacementImage();
            Guid runId = Guid.NewGuid();
            GenerationReference reference = (await store.SaveAsync(runId,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            string historyPath = store.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a stored image.");
            string artPath = Path.Combine(paths.ArtDirectory, Path.GetFileName(historyPath));
            HashSet<string> hashes = [reference.Hash];
            HashSet<string> empty = [];

            using (FileStream opened = new(historyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                await store.PromoteAsync(runId, new GenerationReference[] { reference }, CancellationToken.None);
                await store.SynchronizeAsync(hashes, hashes, CancellationToken.None);
                store.Release(runId);

                TrustedPathGuard.EnsureOpenedFileRemainsInsideDirectory(paths.ArtDirectory, opened.SafeFileHandle,
                    "Expected the original file to move into Art.");
                store.ResolveExistingFilePath(reference).Should().Be(artPath);
                File.Exists(historyPath).Should().BeFalse();

                await store.SynchronizeAsync(empty, hashes, CancellationToken.None);

                TrustedPathGuard.EnsureOpenedFileRemainsInsideDirectory(Path.GetDirectoryName(historyPath)
                    ?? throw new InvalidOperationException("Expected a history directory."), opened.SafeFileHandle,
                    "Expected the original file to move into History.");
                store.ResolveExistingFilePath(reference).Should().Be(historyPath);
                File.Exists(artPath).Should().BeFalse();

                await store.PromoteAsync(runId, new GenerationReference[] { reference }, CancellationToken.None);
                await store.SynchronizeAsync(hashes, hashes, CancellationToken.None);
                store.Release(runId);

                TrustedPathGuard.EnsureOpenedFileRemainsInsideDirectory(paths.ArtDirectory, opened.SafeFileHandle,
                    "Expected the original file to return to Art.");
                (await store.LoadAsync(reference, CancellationToken.None)).Should().BeEquivalentTo(image);
                Directory.GetFiles(paths.ArtDirectory).Should().ContainSingle();
                Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().BeEmpty();
            }

            await store.SynchronizeAsync(empty, empty, CancellationToken.None);

            store.ResolveExistingFilePath(reference).Should().BeNull();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronizeAsync_WithAnActiveReader_DefersRelocationOrDeletionUntilReadingEnds(bool retainedInHistory)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));

        try
        {
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            Guid runId = Guid.NewGuid();
            GenerationReference reference = (await store.SaveAsync(runId,
                new AttachedImageDto[] { GenerationReferenceStoreTests.CreatePlacementImage() }, CancellationToken.None)).Single();
            HashSet<string> hashes = [reference.Hash];
            HashSet<string> empty = [];
            await store.PromoteAsync(runId, new GenerationReference[] { reference }, CancellationToken.None);
            await store.SynchronizeAsync(hashes, hashes, CancellationToken.None);
            store.Release(runId);
            string artPath = store.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a gallery image.");
            string historyPath = Path.Combine(GenerationReferenceStoreTests.GetHistoryDirectory(root), Path.GetFileName(artPath));
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource timeout = new(PlacementTimeout);
            Task<string?> reading = store.ReadAsync<string>(reference, async (stream, ct) =>
            {
                started.SetResult();
                await finish.Task.WaitAsync(ct);
                byte[] bytes = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(bytes, ct);
                bytes.Should().Equal(GenerationReferenceStoreTests.CreatePlacementImage().Content);

                return "Read completed";
            }, timeout.Token);
            await started.Task.WaitAsync(timeout.Token);

            try
            {
                await store.SynchronizeAsync(empty, retainedInHistory ? hashes : empty, timeout.Token);

                File.Exists(artPath).Should().BeTrue();
                File.Exists(historyPath).Should().BeFalse();
            }
            finally
            {
                finish.TrySetResult();
            }

            (await reading).Should().Be("Read completed");
            await AsyncTestWaiter.WaitForConditionAsync(() => !File.Exists(artPath), timeout.Token);

            File.Exists(historyPath).Should().Be(retainedInHistory);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromoteAsync_WithAnActiveHistoryReader_WaitsOrCancelsWithoutCopyingTheFile(bool cancelPromotion)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            Guid runId = Guid.NewGuid();
            Guid readerId = Guid.NewGuid();
            GenerationReference reference = (await store.SaveAsync(runId,
                new AttachedImageDto[] { GenerationReferenceStoreTests.CreatePlacementImage() }, CancellationToken.None)).Single();
            GenerationReference[] references = [reference];
            await store.RetainReadPathsAsync(readerId, references, CancellationToken.None);
            string? historyPath = store.ResolveExistingFilePath(reference);
            using CancellationTokenSource timeout = new(PlacementTimeout);
            Task promotion = store.PromoteAsync(runId, references, timeout.Token);

            try
            {
                promotion.IsCompleted.Should().BeFalse();
                store.ResolveExistingFilePath(reference).Should().Be(historyPath);
                Directory.Exists(paths.ArtDirectory).Should().BeFalse();

                if (cancelPromotion)
                {
                    await timeout.CancelAsync();
                    Func<Task> pending = () => promotion;
                    await pending.Should().ThrowAsync<OperationCanceledException>();
                    store.ResolveExistingFilePath(reference).Should().Be(historyPath);
                }
                else
                {
                    store.Release(readerId);
                    await promotion;

                    File.Exists(historyPath).Should().BeFalse();
                    Directory.GetFiles(paths.ArtDirectory).Should().ContainSingle();
                }
            }
            finally
            {
                store.Release(readerId);
                store.Release(runId);
            }
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromoteAsync_WithAnExistingTarget_RemovesOnlyVerifiedDuplicates(bool corruptTarget)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationReferenceStoreTests));

        try
        {
            AtomicArtDataPathProvider paths = new(root);
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = GenerationReferenceStoreTests.CreatePlacementImage();
            Guid runId = Guid.NewGuid();
            GenerationReference reference = (await store.SaveAsync(runId,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            string historyPath = store.ResolveExistingFilePath(reference)
                ?? throw new InvalidOperationException("Expected a stored image.");
            Directory.CreateDirectory(paths.ArtDirectory);
            string artPath = Path.Combine(paths.ArtDirectory, Path.GetFileName(historyPath));
            byte[] targetContent = corruptTarget ? new byte[] { 0 } : image.Content;
            await File.WriteAllBytesAsync(artPath, targetContent);

            try
            {
                Func<Task> promote = () => store.PromoteAsync(runId, new GenerationReference[] { reference }, CancellationToken.None);

                if (corruptTarget)
                {
                    await promote.Should().ThrowAsync<IOException>();
                    (await File.ReadAllBytesAsync(historyPath)).Should().Equal(image.Content);
                }
                else
                {
                    await promote();
                    File.Exists(historyPath).Should().BeFalse();
                }

                (await File.ReadAllBytesAsync(artPath)).Should().Equal(targetContent);
            }
            finally
            {
                store.Release(runId);
            }
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    private static AttachedImageDto CreatePlacementImage()
    {
        return new AttachedImageDto("image.png", GenerationImageContentTypes.Png,
            GalleryThumbnailTestImages.CreatePngBytes(120, 80));
    }
}
