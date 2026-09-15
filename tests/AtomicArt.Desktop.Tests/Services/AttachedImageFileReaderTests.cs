using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class AttachedImageFileReaderTests
{
    [Fact]
    public async Task CreateInput_WithFilePath_ReadsFileWithoutChangingItsContent()
    {
        string filePath = CreateTemporaryFile(GenerationImageFileSignatures.Png.ToArray());

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
            using ImageAttachmentInput input = reader.CreateInput(filePath, maxInputBytes: 1024);

            AttachedImageDto? image = await input.ReadAsync(CancellationToken.None);

            image.Should().NotBeNull();
            image!.FileName.Should().Be(Path.GetFileName(filePath));
            image.ContentType.Should().Be(GenerationImageContentTypes.Png);
            image.Content.Should().Equal(GenerationImageFileSignatures.Png.ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CreateInput_WithOversizedFilePath_RejectsFileBeforeReading()
    {
        string filePath = CreateTemporaryFile([1, 2, 3, 4]);

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
            using ImageAttachmentInput input = reader.CreateInput(filePath, maxInputBytes: 3);

            Func<Task> action = async () => await input.ReadAsync(CancellationToken.None);

            await action.Should().ThrowAsync<InvalidDataException>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string CreateTemporaryFile(byte[] content)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Tests",
            nameof(AttachedImageFileReaderTests));
        Directory.CreateDirectory(directoryPath);
        string filePath = Path.Combine(directoryPath, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(filePath, content);

        return filePath;
    }
}
