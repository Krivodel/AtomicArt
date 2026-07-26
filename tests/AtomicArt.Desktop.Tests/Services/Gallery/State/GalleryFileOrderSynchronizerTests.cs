using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryFileOrderSynchronizerTests
{
    private static readonly Guid BatchId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime GalleryOrderTimestampUtc = new(
        2026,
        7,
        26,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task SynchronizeAsync_WithManagedImage_SetsSortableFileDates()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryFileOrderSynchronizerTests),
            nameof(SynchronizeAsync_WithManagedImage_SetsSortableFileDates));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            Directory.CreateDirectory(pathProvider.ArtDirectory);
            GenerationImageFileNamePolicy fileNamePolicy = new();
            string fileName = fileNamePolicy.BuildFileName(
                BatchId,
                ItemId,
                ".png");
            string imagePath = Path.Combine(pathProvider.ArtDirectory, fileName);
            await File.WriteAllBytesAsync(
                imagePath,
                GenerationImageTestData.ValidPngBytes);
            GalleryItemState item = GalleryItemStateTestFactory.CreateGenerated(
                id: ItemId,
                galleryOrderTimestampUtc: GalleryOrderTimestampUtc,
                imagePath: Path.Combine("Art", fileName));
            GalleryFileOrderSynchronizer synchronizer = CreateSynchronizer(
                pathProvider,
                fileNamePolicy);

            await synchronizer.SynchronizeAsync(
                [item],
                CancellationToken.None);

            File.GetLastWriteTimeUtc(imagePath).Should()
                .Be(GalleryOrderTimestampUtc);

            if (OperatingSystem.IsWindows())
            {
                File.GetCreationTimeUtc(imagePath).Should()
                    .Be(GalleryOrderTimestampUtc);
            }
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SynchronizeAsync_WithFileOwnedByAnotherItem_DoesNotChangeDates()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryFileOrderSynchronizerTests),
            nameof(SynchronizeAsync_WithFileOwnedByAnotherItem_DoesNotChangeDates));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            Directory.CreateDirectory(pathProvider.ArtDirectory);
            GenerationImageFileNamePolicy fileNamePolicy = new();
            string fileName = fileNamePolicy.BuildFileName(
                BatchId,
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ".png");
            string imagePath = Path.Combine(pathProvider.ArtDirectory, fileName);
            await File.WriteAllBytesAsync(
                imagePath,
                GenerationImageTestData.ValidPngBytes);
            DateTime originalTimestampUtc = new(
                2026,
                7,
                25,
                12,
                0,
                0,
                DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(imagePath, originalTimestampUtc);
            GalleryItemState item = GalleryItemStateTestFactory.CreateGenerated(
                id: ItemId,
                galleryOrderTimestampUtc: GalleryOrderTimestampUtc,
                imagePath: Path.Combine("Art", fileName));
            GalleryFileOrderSynchronizer synchronizer = CreateSynchronizer(
                pathProvider,
                fileNamePolicy);

            await synchronizer.SynchronizeAsync(
                [item],
                CancellationToken.None);

            File.GetLastWriteTimeUtc(imagePath).Should()
                .Be(originalTimestampUtc);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static GalleryFileOrderSynchronizer CreateSynchronizer(
        AtomicArtDataPathProvider pathProvider,
        GenerationImageFileNamePolicy fileNamePolicy)
    {
        GalleryStatePathConverter pathConverter = new(
            pathProvider,
            new RejectingTrustedImageFileService(),
            NullLogger<GalleryStatePathConverter>.Instance);

        return new GalleryFileOrderSynchronizer(
            new DataRootAccessCoordinator(),
            pathConverter,
            fileNamePolicy,
            NullLogger<GalleryFileOrderSynchronizer>.Instance);
    }
}
