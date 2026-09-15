using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ClipboardImageServiceTests
{
    private const int MaxInputBytes = 1024;
    private const string FileName = "copied.png";

    private static readonly byte[] PngContent = GenerationImageFileSignatures.Png.ToArray();
    private static readonly byte[] JpegContent = GenerationImageFileSignatures.Jpeg.ToArray();

    [Fact]
    public async Task TryGetImageAsync_WithFileAndPngFormats_ReturnsFileInput()
    {
        Mock<IStorageFile> fileMock = new();
        fileMock.SetupGet(file => file.Name).Returns(FileName);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));
        dataTransfer.Add(CreatePngTransferItem());

        ImageAttachmentInput actualInput = await GetRequiredImageInputAsync(
            dataTransfer,
            "Clipboard file input should be created.");

        actualInput.FileName.Should().Be(FileName);
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipboardImageService>.Instance);
        service.Attach(clipboardMock.Object);

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
