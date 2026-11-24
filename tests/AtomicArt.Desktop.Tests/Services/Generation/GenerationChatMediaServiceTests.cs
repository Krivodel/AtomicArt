using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationChatMediaServiceTests
{
    [Theory]
    [InlineData("Art", false, true)]
    [InlineData("Art", true, false)]
    [InlineData("Thumbnails", false, false)]
    public async Task ReadResultAsync_WithTrustedAndUntrustedImages_ValidatesContentWithoutWritingReferences(
        string directory, bool invalidSignature, bool expectedImage)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(GenerationChatMediaServiceTests));

        try
        {
            string folder = Path.Combine(root, directory);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "result.png");
            byte[] content = invalidSignature ? new byte[] { 1, 2, 3 } : GalleryThumbnailTestImages.CreatePngBytes(120, 80);
            await File.WriteAllBytesAsync(path, content);
            Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
            GenerationChatMediaService service = GenerationChatMediaServiceTestFactory.Create(
                references.Object, new AtomicArtDataPathProvider(root), content.Length);

            AttachedImageDto? image = await service.ReadResultAsync(path, CancellationToken.None);

            (image is not null).Should().Be(expectedImage);
            image?.Content.Should().Equal(content);
            references.VerifyNoOtherCalls();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }
}
