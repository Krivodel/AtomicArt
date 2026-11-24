using System.Text.Json;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation.LocalCodex;

public sealed class LocalCodexInputPreparationTests
{
    private static readonly DateTime TimestampUtc = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PrepareAsync_WithRepeatedHistoryAndParallelRequests_ReusesOneFileWithoutReadingImageBytes()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(LocalCodexInputPreparationTests));

        try
        {
            GenerationReferenceStore store = GenerationReferenceStoreTests.CreateStore(root);
            AttachedImageDto image = new("reference.png", GenerationImageContentTypes.Png,
                GalleryThumbnailTestImages.CreatePngBytes(120, 80));
            Guid originalLease = Guid.NewGuid();
            GenerationReference reference = (await store.SaveAsync(originalLease,
                new AttachedImageDto[] { image }, CancellationToken.None)).Single();
            string path = store.ResolveExistingFilePath(reference) ?? throw new InvalidOperationException("Expected an image.");
            File.SetLastWriteTimeUtc(path, TimestampUtc);
            Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
            references.Setup(value => value.RetainReadPathsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<GenerationReference>>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, IEnumerable<GenerationReference> images, CancellationToken ct) => store.RetainReadPathsAsync(id, images, ct));
            references.Setup(value => value.SaveAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<AttachedImageDto>>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, IReadOnlyList<AttachedImageDto> images, CancellationToken ct) => store.SaveAsync(id, images, ct));
            references.Setup(value => value.GetExistingFilePathAsync(It.IsAny<GenerationReference>(), It.IsAny<CancellationToken>()))
                .Returns((GenerationReference saved, CancellationToken ct) => store.GetExistingFilePathAsync(saved, ct));
            references.Setup(value => value.Release(It.IsAny<Guid>())).Callback<Guid>(store.Release);
            GenerationChatHistory history = new(new GenerationChatMessage[]
            {
                new GenerationChatMessage(false, "Reference", TimestampUtc, new GenerationReference[] { reference }),
                new GenerationChatMessage(true, "Previous result", TimestampUtc, new GenerationReference[] { reference })
            });
            ImageGenerationRequestDto request = new(LocalCodexImageModel.Id, "Continue", GenerationAspectRatios.Auto,
                LocalCodexImageModel.Automatic, LocalCodexImageModel.Option.Temperature.Default, 1,
                new AttachedImageDto[] { image });
            using LocalCodexInputPreparation first = new(references.Object, GenerationImageFormatRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
            using LocalCodexInputPreparation second = new(references.Object, GenerationImageFormatRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);

            await Task.WhenAll(first.PrepareAsync(request, history, CancellationToken.None),
                second.PrepareAsync(request, history, CancellationToken.None));
            store.Release(originalLease);
            await store.PruneAsync(new HashSet<string>(), CancellationToken.None);
            first.Dispose();
            await store.PruneAsync(new HashSet<string>(), CancellationToken.None);

            File.Exists(path).Should().BeTrue();
            File.GetLastWriteTimeUtc(path).Should().Be(TimestampUtc);
            Directory.GetFiles(GenerationReferenceStoreTests.GetHistoryDirectory(root)).Should().ContainSingle();
            JsonElement inputs = JsonSerializer.SerializeToElement(second.Inputs);
            inputs.EnumerateArray().Where(input => input.GetProperty("type").GetString() == "localImage")
                .Should().HaveCount(3).And.OnlyContain(input => input.GetProperty("path").GetString() == path);

            second.Dispose();
            await store.PruneAsync(new HashSet<string>(), CancellationToken.None);

            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }
}
