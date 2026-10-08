using Avalonia.Platform.Storage;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Tests.Common;

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
            AttachedImageDto actualImage = image
                ?? throw new InvalidOperationException("The file image should be available.");
            actualImage.FileName.Should().Be(Path.GetFileName(filePath));
            actualImage.ContentType.Should().Be(GenerationImageContentTypes.Png);
            actualImage.Content.Should().Equal(GenerationImageFileSignatures.Png.ToArray());
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

    [Fact]
    public void CaptureInput_WhenDisposedWithoutReading_ReleasesFileHandle()
    {
        string filePath = CreateTemporaryFile(GenerationImageFileSignatures.Png.ToArray());

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
            Mock<IStorageFile> fileMock = StorageFileTestData.CreateFile(filePath);
            using ImageAttachmentInput input = reader.CaptureInput(fileMock.Object, maxInputBytes: 1024);
            Action openForWriting = () =>
            {
                using FileStream writer = File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
            };
            openForWriting.Should().Throw<IOException>();

            input.Dispose();

            openForWriting.Should().NotThrow();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CaptureInput_WithOversizedFile_DefersErrorAndReleasesFileHandle()
    {
        string filePath = CreateTemporaryFile(GenerationImageFileSignatures.Png.ToArray());

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
            Mock<IStorageFile> fileMock = StorageFileTestData.CreateFile(filePath);
            using ImageAttachmentInput input = reader.CaptureInput(fileMock.Object, maxInputBytes: 1);
            Func<Task> read = () => input.ReadAsync(CancellationToken.None);

            await read.Should().ThrowAsync<InvalidDataException>();

            using FileStream writer = File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CaptureInput_WithStorageProviderFile_PreservesProviderReading()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        Mock<IStorageFile> fileMock = new();
        fileMock.SetupGet(file => file.Name).Returns("provider.png");
        fileMock.SetupGet(file => file.Path).Returns(new Uri("content://images/provider.png"));
        fileMock.Setup(file => file.GetBasicPropertiesAsync()).ReturnsAsync(new StorageItemProperties());
        fileMock.Setup(file => file.OpenReadAsync()).ReturnsAsync(new MemoryStream(content));
        AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
        using ImageAttachmentInput input = reader.CaptureInput(fileMock.Object, maxInputBytes: 1024);

        AttachedImageDto? image = await input.ReadAsync(CancellationToken.None);

        image.Should().NotBeNull();
        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("The storage provider image should be available.");
        actualImage.Content.Should().Equal(content);
        fileMock.Verify(file => file.OpenReadAsync(), Times.Once);
    }

    [Theory]
    [InlineData("missing.png")]
    [InlineData("missing-directory/missing.png")]
    public void TryCaptureInput_WithMissingFile_ReturnsNoInput(string relativePath)
    {
        using TemporaryDirectory directory = new(
            typeof(AttachedImageFileReaderTests),
            nameof(TryCaptureInput_WithMissingFile_ReturnsNoInput));
        string filePath = Path.Combine(directory.DirectoryPath, relativePath);
        AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());

        using ImageAttachmentInput? input = reader.TryCaptureInput(filePath, maxInputBytes: 1024);

        input.Should().BeNull();
    }

    [Fact]
    public void TryCaptureInput_WithEmptyFile_ReturnsNoInputAndReleasesFileHandle()
    {
        string filePath = CreateTemporaryFile(Array.Empty<byte>());

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());

            using ImageAttachmentInput? input = reader.TryCaptureInput(filePath, maxInputBytes: 1024);

            input.Should().BeNull();
            using FileStream writer = File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task TryCaptureInput_WhenFileIsDeletedBeforeReading_ReadsCapturedContent()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        string filePath = CreateTemporaryFile(content);

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());

            using ImageAttachmentInput input = reader.TryCaptureInput(filePath, maxInputBytes: 1024)
                ?? throw new InvalidOperationException("The clipboard file should be captured.");
            File.Delete(filePath);
            AttachedImageDto? image = await input.ReadAsync(CancellationToken.None);

            File.Exists(filePath).Should().BeFalse();
            image.Should().NotBeNull();
            image?.Content.Should().Equal(content);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void TryCaptureInput_WhenDisposedWithoutReading_ReleasesFileHandle()
    {
        string filePath = CreateTemporaryFile(GenerationImageFileSignatures.Png.ToArray());

        try
        {
            AttachedImageFileReader reader = new(new AttachedImageSignatureValidator());
            using ImageAttachmentInput input = reader.TryCaptureInput(filePath, maxInputBytes: 1024)
                ?? throw new InvalidOperationException("The clipboard file should be captured.");
            Action openForWriting = () =>
            {
                using FileStream writer = File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
            };
            openForWriting.Should().Throw<IOException>();

            input.Dispose();

            openForWriting.Should().NotThrow();
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
