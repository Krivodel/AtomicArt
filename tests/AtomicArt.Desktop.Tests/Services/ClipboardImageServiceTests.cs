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

    [Fact]
    public async Task TryGetImageAsync_WithFileAndPngFormats_ReturnsFileInput()
    {
        Mock<IStorageFile> fileMock = new();
        fileMock.SetupGet(file => file.Name).Returns(FileName);
        DataTransferItem pngItem = new();
        DataFormat<byte[]> pngFormat = DataFormat.CreateBytesPlatformFormat(
            PicaClipboardFormats.WindowsPng);
        pngItem.Set(pngFormat, PngContent);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));
        dataTransfer.Add(pngItem);
        ClipboardImageService service = CreateService(dataTransfer);

        ImageAttachmentInput? input = await service.TryGetImageAsync(
            MaxInputBytes,
            CancellationToken.None);

        ImageAttachmentInput actualInput = input
            ?? throw new InvalidOperationException("Clipboard file input should be created.");
        actualInput.FileName.Should().Be(FileName);
    }

    [Fact]
    public async Task TryGetImageAsync_WithPicaPngFormat_ReturnsPngInput()
    {
        DataTransferItem item = new();
        DataFormat<byte[]> pngFormat = DataFormat.CreateBytesPlatformFormat(
            PicaClipboardFormats.WindowsPng);
        item.Set(pngFormat, PngContent);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(item);
        ClipboardImageService service = CreateService(dataTransfer);

        ImageAttachmentInput? input = await service.TryGetImageAsync(
            MaxInputBytes,
            CancellationToken.None);
        ImageAttachmentInput actualInput = input
            ?? throw new InvalidOperationException("Clipboard PNG input should be created.");
        AttachedImageDto? image = await actualInput.ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Clipboard PNG should be read.");
        actualImage.FileName.Should().Be("clipboard.png");
        actualImage.ContentType.Should().Be(PicaImageFormats.PngContentType);
        actualImage.Content.Should().Equal(PngContent);
    }

    private static ClipboardImageService CreateService(IAsyncDataTransfer dataTransfer)
    {
        Mock<IClipboard> clipboardMock = new();
        clipboardMock
            .Setup(clipboard => clipboard.TryGetDataAsync())
            .ReturnsAsync(dataTransfer);
        ClipboardImageService service = new(
            new AttachedImageFileReader(new AttachedImageSignatureValidator()));
        service.Attach(clipboardMock.Object);

        return service;
    }
}
