using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Tests.Common;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ClipboardImageServiceTests
{
    private const int MaxInputBytes = 1024;
    private const string FileName = "copied.png";

    private static readonly byte[] PngContent = GenerationImageFileSignatures.Png.ToArray();
    private static readonly byte[] JpegContent = GenerationImageFileSignatures.Jpeg.ToArray();

    [Fact]
    public async Task SetImageAsync_WithImagePath_DelegatesToPicaCopier()
    {
        Mock<IClipboard> clipboardMock = new();
        Mock<IStorageProvider> storageProviderMock = new();
        Mock<IClipboardImageCopier> copierMock = new();
        copierMock
            .Setup(copier => copier.CopyFileAsync(
                "source.jpg",
                clipboardMock.Object,
                storageProviderMock.Object,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ClipboardImageService service = new(
            new AttachedImageFileReader(new AttachedImageSignatureValidator()),
            new StubPlatformClipboardImageReader(fallbackInput: null),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipboardImageService>.Instance,
            copierMock.Object);
        service.Attach(clipboardMock.Object, storageProviderMock.Object);

        await service.SetImageAsync("source.jpg", CancellationToken.None);

        copierMock.Verify(copier => copier.CopyFileAsync(
            "source.jpg",
            clipboardMock.Object,
            storageProviderMock.Object,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryGetImageAsync_WithFileAndPngFormats_ReturnsFileInput()
    {
        using TemporaryDirectory directory = new(
            typeof(ClipboardImageServiceTests),
            nameof(TryGetImageAsync_WithFileAndPngFormats_ReturnsFileInput));
        string filePath = Path.Combine(directory.DirectoryPath, FileName);
        await File.WriteAllBytesAsync(filePath, JpegContent);
        Mock<IStorageFile> fileMock = StorageFileTestData.CreateFile(filePath);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));
        dataTransfer.Add(CreatePngTransferItem());

        using ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard file input should be created.");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);

        actualInput.FileName.Should().Be(FileName);
        image.Should().NotBeNull();
        image?.Content.Should().Equal(JpegContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryGetImageAsync_WithUnavailableFileAndPngFormat_ReturnsClipboardPixels(
        bool fileWasDeleted)
    {
        using TemporaryDirectory directory = new(
            typeof(ClipboardImageServiceTests),
            nameof(TryGetImageAsync_WithUnavailableFileAndPngFormat_ReturnsClipboardPixels));
        string filePath = Path.Combine(directory.DirectoryPath, FileName);
        await File.WriteAllBytesAsync(filePath, PngContent);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(StorageFileTestData.CreateFile(filePath).Object));
        dataTransfer.Add(CreatePngTransferItem());

        if (fileWasDeleted)
        {
            File.Delete(filePath);
        }
        else
        {
            await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
        }

        using ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard pixels should survive removal of the copied file.");

        actualInput.FileName.Should().Be("clipboard.png");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);
        image.Should().NotBeNull();
        image?.Content.Should().Equal(PngContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryGetImageAsync_WithUnavailableFileAndNoAvaloniaImage_UsesPlatformPixels(
        bool fileWasDeleted)
    {
        using TemporaryDirectory directory = new(
            typeof(ClipboardImageServiceTests),
            nameof(TryGetImageAsync_WithUnavailableFileAndNoAvaloniaImage_UsesPlatformPixels));
        string filePath = Path.Combine(directory.DirectoryPath, FileName);
        await File.WriteAllBytesAsync(filePath, PngContent);
        AttachedImageDto fallbackImage = new(
            "fallback.png",
            GenerationImageContentTypes.Png,
            PngContent);
        StubPlatformClipboardImageReader fallbackReader = new(
            ImageAttachmentInput.FromImage(fallbackImage));
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(StorageFileTestData.CreateFile(filePath).Object));
        ClipboardImageService service = CreateService(dataTransfer, fallbackReader);

        if (fileWasDeleted)
        {
            File.Delete(filePath);
        }
        else
        {
            await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
        }

        using ImageAttachmentInput? input = await service.TryGetImageAsync(
            MaxInputBytes,
            CancellationToken.None);

        fallbackReader.CallCount.Should().Be(1);
        AttachedImageDto? actualImage = input is null
            ? null
            : await input.ReadAsync(CancellationToken.None);
        actualImage.Should().BeEquivalentTo(fallbackImage);
    }

    [Fact]
    public async Task TryGetImageAsync_WhenFileIsDeletedBeforeDeferredRead_PreservesOriginalContent()
    {
        using TemporaryDirectory directory = new(
            typeof(ClipboardImageServiceTests),
            nameof(TryGetImageAsync_WhenFileIsDeletedBeforeDeferredRead_PreservesOriginalContent));
        string filePath = Path.Combine(directory.DirectoryPath, FileName);
        await File.WriteAllBytesAsync(filePath, JpegContent);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(StorageFileTestData.CreateFile(filePath).Object));
        dataTransfer.Add(CreatePngTransferItem());

        using ImageAttachmentInput input = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard file input should be captured.");
        File.Delete(filePath);
        AttachedImageDto? image = await input.ReadAsync(CancellationToken.None);

        File.Exists(filePath).Should().BeFalse();
        image.Should().NotBeNull();
        image?.Content.Should().Equal(JpegContent);
    }

    [Fact]
    public async Task TryGetImageAsync_WithOversizedFileAndPngFormat_PreservesInputLimit()
    {
        using TemporaryDirectory directory = new(
            typeof(ClipboardImageServiceTests),
            nameof(TryGetImageAsync_WithOversizedFileAndPngFormat_PreservesInputLimit));
        string filePath = Path.Combine(directory.DirectoryPath, FileName);
        await File.WriteAllBytesAsync(filePath, new byte[MaxInputBytes + 1]);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(StorageFileTestData.CreateFile(filePath).Object));
        dataTransfer.Add(CreatePngTransferItem());
        using ImageAttachmentInput input = await GetRequiredImageInputAsync(
            dataTransfer,
            "An oversized clipboard file should retain its validation error.");
        Func<Task> read = () => input.ReadAsync(CancellationToken.None);

        await read.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task TryGetImageAsync_WithPicaPngFormat_ReturnsPngInput()
    {
        DataTransfer dataTransfer = new();
        dataTransfer.Add(CreatePngTransferItem());

        ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard PNG input should be created.");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Clipboard PNG should be read.");
        actualImage.FileName.Should().Be("clipboard.png");
        actualImage.ContentType.Should().Be(PicaImageFormats.PngContentType);
        actualImage.Content.Should().Equal(PngContent);
    }

    [Fact]
    public async Task TryGetImageAsync_WithJpegMimeFormat_ReturnsJpegInput()
    {
        DataFormat<byte[]> jpegFormat = DataFormat.CreateBytesPlatformFormat(
            GenerationImageContentTypes.Jpeg);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(jpegFormat, JpegContent));

        ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard JPEG input should be created.");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Clipboard JPEG should be read.");
        actualImage.FileName.Should().Be("clipboard.jpg");
        actualImage.ContentType.Should().Be(GenerationImageContentTypes.Jpeg);
        actualImage.Content.Should().Equal(JpegContent);
    }

    [Fact]
    public async Task TryGetImageAsync_WithOtherImageMimeFormat_ReturnsConvertibleInput()
    {
        const string contentType = "image/vnd.atomicart-test";
        byte[] content = [0x01, 0x02, 0x03];
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(contentType);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(format, content));

        ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard MIME image input should be created.");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Clipboard MIME image should be read.");
        actualImage.FileName.Should().Be("clipboard.img");
        actualImage.ContentType.Should().Be(contentType);
        actualImage.Content.Should().Equal(content);
    }

    [Fact]
    public async Task TryGetImageAsync_WithoutAvaloniaImage_UsesPlatformFallback()
    {
        AttachedImageDto fallbackImage = new(
            "fallback.png",
            GenerationImageContentTypes.Png,
            PngContent);
        StubPlatformClipboardImageReader fallbackReader = new(
            ImageAttachmentInput.FromImage(fallbackImage));
        DataTransfer dataTransfer = new();
        ClipboardImageService service = CreateService(dataTransfer, fallbackReader);

        ImageAttachmentInput? input = await service.TryGetImageAsync(
            MaxInputBytes,
            CancellationToken.None);
        AttachedImageDto? actualImage = input is null
            ? null
            : await input.ReadAsync(CancellationToken.None);

        actualImage.Should().BeEquivalentTo(fallbackImage);
        fallbackReader.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task TryGetImageAsync_WithAvaloniaImage_DoesNotUsePlatformFallback()
    {
        StubPlatformClipboardImageReader fallbackReader = new(fallbackInput: null);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(CreatePngTransferItem());
        ClipboardImageService service = CreateService(dataTransfer, fallbackReader);

        ImageAttachmentInput? input = await service.TryGetImageAsync(
            MaxInputBytes,
            CancellationToken.None);

        input.Should().NotBeNull();
        fallbackReader.CallCount.Should().Be(0);
    }

    private static DataTransferItem CreatePngTransferItem()
    {
        DataTransferItem item = new();
        DataFormat<byte[]> pngFormat = DataFormat.CreateBytesPlatformFormat(
            PicaClipboardFormats.WindowsPng);
        item.Set(pngFormat, PngContent);

        return item;
    }

    private static ClipboardImageService CreateService(IAsyncDataTransfer dataTransfer)
    {
        return CreateService(
            dataTransfer,
            new StubPlatformClipboardImageReader(fallbackInput: null));
    }

    private static ClipboardImageService CreateService(
        IAsyncDataTransfer dataTransfer,
        IPlatformClipboardImageReader fallbackReader)
    {
        Mock<IClipboard> clipboardMock = new();
        clipboardMock
            .Setup(clipboard => clipboard.TryGetDataAsync())
            .ReturnsAsync(dataTransfer);
        ClipboardImageService service = new(
            new AttachedImageFileReader(new AttachedImageSignatureValidator()),
            fallbackReader,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipboardImageService>.Instance,
            new Mock<IClipboardImageCopier>().Object);
        service.Attach(clipboardMock.Object, new Mock<IStorageProvider>().Object);

        return service;
    }

    private static async Task<ImageAttachmentInput> GetRequiredImageInputAsync(
        IAsyncDataTransfer dataTransfer,
        string missingInputMessage)
    {
        ClipboardImageService service = CreateService(dataTransfer);

        ImageAttachmentInput? input = await service
            .TryGetImageAsync(MaxInputBytes, CancellationToken.None)
            .ConfigureAwait(false);

        return input
            ?? throw new InvalidOperationException(missingInputMessage);
    }

    private sealed class StubPlatformClipboardImageReader(
        ImageAttachmentInput? fallbackInput) : IPlatformClipboardImageReader
    {
        public int CallCount { get; private set; }

        public Task<ImageAttachmentInput?> TryGetImageAsync(
            int maxInputBytes,
            CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
            ct.ThrowIfCancellationRequested();
            CallCount++;

            return Task.FromResult(fallbackInput);
        }
    }
}
