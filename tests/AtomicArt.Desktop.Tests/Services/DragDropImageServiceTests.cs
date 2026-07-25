using System.Net;
using System.Net.Http.Headers;

using Avalonia.Input;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static DragDropImageService CreateService(HttpMessageHandler handler)
    {
        AttachedImageSignatureValidator signatureValidator = new();
        ExternalImageAttachmentReader externalImageReader = new(
            new HttpClient(handler),
            signatureValidator,
            NullLogger<ExternalImageAttachmentReader>.Instance);

        return new DragDropImageService(
            new AttachedImageFileReader(signatureValidator),
            externalImageReader);
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
