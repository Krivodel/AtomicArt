using System.Net;
using System.Net.Http.Headers;

using Avalonia.Input;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DragDropImageServiceTests
{
    private const int MaxInputBytes = 1024;

    [Fact]
    public async Task ExtractImagesAsync_WithEncodedWebp_ReturnsEncodedInput()
    {
        byte[] content = CreateWebpContent();
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(
            GenerationImageContentTypes.Webp);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(format, content));
        DragDropImageService service = CreateService(new ImageHttpMessageHandler(content));

        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer,
            MaxInputBytes,
            CancellationToken.None);
        AttachedImageDto? image = await inputs.Single().ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Dropped WebP should be read.");
        actualImage.FileName.Should().Be("dropped-image.webp");
        actualImage.ContentType.Should().Be(GenerationImageContentTypes.Webp);
        actualImage.Content.Should().Equal(content);
    }

    [Fact]
    public async Task ExtractImagesAsync_WithBrowserUrl_DefersDownloadUntilRead()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        ImageHttpMessageHandler handler = new(content);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateText(
            "https://images.atomicart.test/reference.png"));
        DragDropImageService service = CreateService(handler);

        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer,
            MaxInputBytes,
            CancellationToken.None);

        handler.RequestCount.Should().Be(0);

        AttachedImageDto? image = await inputs.Single().ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Dropped browser image should be read.");
        handler.RequestCount.Should().Be(1);
        actualImage.FileName.Should().Be("reference.png");
        actualImage.ContentType.Should().Be(GenerationImageContentTypes.Png);
        actualImage.Content.Should().Equal(content);
    }

    [Fact]
    public async Task ExtractImagesAsync_WithOversizedRemoteImage_ThrowsInvalidDataException()
    {
        byte[] content = new byte[MaxInputBytes + 1];
        ImageHttpMessageHandler handler = new(content);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateText(
            "https://images.atomicart.test/reference.png"));
        DragDropImageService service = CreateService(handler);
        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer,
            MaxInputBytes,
            CancellationToken.None);

        Func<Task> act = () => inputs.Single().ReadAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ExtractImagesAsync_WithImageDataUri_ReturnsDecodedInput()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        string dataUri = string.Concat(
            "data:image/png;base64,",
            Convert.ToBase64String(content));
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateText(dataUri));
        DragDropImageService service = CreateService(
            new ImageHttpMessageHandler(content));

        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer,
            MaxInputBytes,
            CancellationToken.None);
        AttachedImageDto? image = await inputs.Single().ReadAsync(CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException("Dropped data image should be read.");
        actualImage.FileName.Should().Be("dropped-image.png");
        actualImage.ContentType.Should().Be(GenerationImageContentTypes.Png);
        actualImage.Content.Should().Equal(content);
    }

    [Fact]
    public async Task ExtractImagesAsync_WithVirtualFile_PrefersVirtualFile()
    {
        byte[] virtualContent = GenerationImageFileSignatures.Png.ToArray();
        ImageAttachmentInput virtualInput = ImageAttachmentInput.FromImage(
            new AttachedImageDto(
                "virtual.png",
                GenerationImageContentTypes.Png,
                virtualContent));
        VirtualFileDropInputSession session = new();
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(
            DataFormat.CreateBytesPlatformFormat(
                GenerationImageContentTypes.Webp),
            CreateWebpContent()));
        DragDropImageService service = CreateService(
            new ImageHttpMessageHandler(virtualContent),
            session);

        using IDisposable scope = session.Begin(
            new ImageAttachmentInput[] { virtualInput });
        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer,
            MaxInputBytes,
            CancellationToken.None);
        AttachedImageDto? image = await inputs.Single().ReadAsync(
            CancellationToken.None);

        AttachedImageDto actualImage = image
            ?? throw new InvalidOperationException(
                "The virtual image should be available.");
        actualImage.FileName.Should().Be("virtual.png");
        actualImage.Content.Should().Equal(virtualContent);
    }

    [Fact]
    public async Task ExtractImagesAsync_WithVirtualFileAndStoragePath_PrefersCapturedContents()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        using ImageAttachmentInput virtualInput = ImageAttachmentInput.FromImage(
            new AttachedImageDto("archive.png", GenerationImageContentTypes.Png, content));
        VirtualFileDropInputSession session = new();
        Mock<IStorageFile> fileMock = new(MockBehavior.Strict);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));
        DragDropImageService service = CreateService(new ImageHttpMessageHandler(content), session);
        using IDisposable scope = session.Begin(new ImageAttachmentInput[] { virtualInput });

        IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
            dataTransfer, MaxInputBytes, CancellationToken.None);

        inputs.Should().ContainSingle().Which.Should().BeSameAs(virtualInput);
        session.TryTakeInputs(out _).Should().BeFalse();
        fileMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExtractImagesAsync_WhenArchiverDeletesTemporaryFile_ReturnsOriginalImage()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        string filePath = Path.Combine(Path.GetTempPath(), $"AtomicArt-drop-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(filePath, content);
        Mock<IStorageFile> fileMock = StorageFileTestData.CreateFile(filePath);
        fileMock.SetupGet(file => file.Name).Returns("archive.png");
        fileMock.Setup(file => file.GetBasicPropertiesAsync())
            .ReturnsAsync(new StorageItemProperties((ulong)content.Length));
        fileMock.Setup(file => file.OpenReadAsync())
            .Returns(() => Task.FromResult<Stream>(File.OpenRead(filePath)));
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(fileMock.Object));
        DragDropImageService service = CreateService(new ImageHttpMessageHandler(content));

        try
        {
            IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
                dataTransfer, MaxInputBytes, CancellationToken.None);
            using ImageAttachmentInput input = inputs.Single();
            File.Delete(filePath);
            AttachedImageDto? image = await input.ReadAsync(CancellationToken.None);

            File.Exists(filePath).Should().BeFalse();
            image.Should().NotBeNull();
            AttachedImageDto actualImage = image
                ?? throw new InvalidOperationException("The dropped image should survive archive cleanup.");
            actualImage.FileName.Should().Be("archive.png");
            actualImage.ContentType.Should().Be(GenerationImageContentTypes.Png);
            actualImage.Content.Should().Equal(content);
            fileMock.Verify(file => file.OpenReadAsync(), Times.Never);
            fileMock.Verify(file => file.GetBasicPropertiesAsync(), Times.Never);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExtractImagesAsync_WithMissingAndValidFiles_PreservesValidImage()
    {
        byte[] content = GenerationImageFileSignatures.Png.ToArray();
        string filePath = Path.Combine(Path.GetTempPath(), $"AtomicArt-drop-{Guid.NewGuid():N}.png");
        string missingPath = Path.ChangeExtension(filePath, ".missing.png");
        File.WriteAllBytes(filePath, content);
        Mock<IStorageFile> missingFile = StorageFileTestData.CreateFile(missingPath);
        Mock<IStorageFile> validFile = StorageFileTestData.CreateFile(filePath);
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(missingFile.Object));
        dataTransfer.Add(DataTransferItem.CreateFile(validFile.Object));
        DragDropImageService service = CreateService(new ImageHttpMessageHandler(content));

        try
        {
            IReadOnlyList<ImageAttachmentInput> inputs = await service.ExtractImagesAsync(
                dataTransfer, MaxInputBytes, CancellationToken.None);
            inputs.Should().HaveCount(2);
            using ImageAttachmentInput missingInput = inputs[0];
            using ImageAttachmentInput validInput = inputs[1];
            Func<Task> readMissingImage = () => missingInput.ReadAsync(CancellationToken.None);
            await readMissingImage.Should().ThrowAsync<FileNotFoundException>();
            AttachedImageDto? image = await validInput.ReadAsync(CancellationToken.None);

            image.Should().NotBeNull();
            AttachedImageDto actualImage = image
                ?? throw new InvalidOperationException("An unavailable entry must not discard the other image.");
            actualImage.Content.Should().Equal(content);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static DragDropImageService CreateService(HttpMessageHandler handler)
    {
        return CreateService(
            handler,
            new VirtualFileDropInputSession());
    }

    private static DragDropImageService CreateService(
        HttpMessageHandler handler,
        IVirtualFileDropInputProvider virtualFileInputProvider)
    {
        AttachedImageSignatureValidator signatureValidator = new();
        ExternalImageAttachmentReader externalImageReader = new(
            new HttpClient(handler),
            signatureValidator,
            NullLogger<ExternalImageAttachmentReader>.Instance,
            TestApiConfiguration.CreateDataTransferOptionsWrapper());

        return new DragDropImageService(
            new AttachedImageFileReader(signatureValidator),
            externalImageReader,
            virtualFileInputProvider,
            NullLogger<DragDropImageService>.Instance);
    }

    private static byte[] CreateWebpContent()
    {
        byte[] content = new byte[12];
        GenerationImageFileSignatures.Riff.CopyTo(content);
        GenerationImageFileSignatures.Webp.CopyTo(
            content.AsSpan(GenerationImageFileSignatures.WebpFormatOffset));

        return content;
    }

    private sealed class ImageHttpMessageHandler(byte[] content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            ByteArrayContent responseContent = new(content);
            responseContent.Headers.ContentType = new MediaTypeHeaderValue(
                GenerationImageContentTypes.Png);

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = responseContent,
                RequestMessage = request
            };

            return Task.FromResult(response);
        }
    }
}
