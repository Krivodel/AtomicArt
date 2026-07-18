using Microsoft.Extensions.Logging.Abstractions;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class PicaViewerSessionTests
{
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PrepareAsync_WithFileThumbnail_PassesThumbnailToPica()
    {
        const string modelId = "model";
        const string imagePath = "images/source.png";
        const string thumbnailPath = "images/thumbnail.webp";
        const string trustedImagePath = "trusted/source.png";
        const string trustedThumbnailPath = "trusted/thumbnail.webp";
        Mock<IClipboardImageWriter> clipboardImageWriterMock = new();
        Mock<ITrustedImageFileService> trustedImageFileServiceMock = new();
        Mock<IGenerationImageFormatRegistry> formatRegistryMock = new();
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        trustedImageFileServiceMock
            .Setup(service => service.GetTrustedImagePath(imagePath, modelId))
            .Returns(trustedImagePath);
        trustedImageFileServiceMock
            .Setup(service => service.GetTrustedImagePathOrDefault(thumbnailPath, modelId))
            .Returns(trustedThumbnailPath);
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(
                new List<GalleryImageViewerItem>
                {
                    new(
                        ItemId,
                        new GalleryFileImageViewerSource(modelId, imagePath, thumbnailPath))
                }),
            ItemId,
            null);

        await using PicaViewerSession session = new(
            clipboardImageWriterMock.Object,
            trustedImageFileServiceMock.Object,
            formatRegistryMock.Object,
            uiThreadDispatcherMock.Object,
            NullLogger<PicaViewerSession>.Instance);
        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest preparedRequest = session.Request
            ?? throw new InvalidOperationException("The Pica request was not prepared.");
        preparedRequest.Items.Should().ContainSingle();
        preparedRequest.Items[0].FilePath.Should().Be(trustedImagePath);
        preparedRequest.Items[0].PreviewFilePath.Should().Be(trustedThumbnailPath);
    }

    [Fact]
    public async Task PrepareAsync_WithAttachedImage_MaterializesFileAndAttachAction()
    {
        Mock<IClipboardImageWriter> clipboardImageWriterMock = new();
        Mock<ITrustedImageFileService> trustedImageFileServiceMock = new();
        Mock<IGenerationImageFormatRegistry> formatRegistryMock = new();
        Mock<IGenerationImageFormat> formatMock = new();
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        formatMock.SetupGet(format => format.Extension).Returns(".png");
        formatRegistryMock
            .Setup(registry => registry.TryGetByContentType(
                GenerationImageContentTypes.Png,
                out It.Ref<IGenerationImageFormat?>.IsAny))
            .Returns((string? _, out IGenerationImageFormat? format) =>
            {
                format = formatMock.Object;
                return true;
            });
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(
            _ => Task.CompletedTask);
        AttachedImageDto image = new(
            "reference.png",
            GenerationImageContentTypes.Png,
            [1, 2, 3]);
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(
                new List<GalleryImageViewerItem>
                {
                    new(ItemId, new GalleryAttachedImageViewerSource(image))
                }),
            ItemId,
            attachCommand);
        string materializedPath;

        await using (PicaViewerSession session = new(
                         clipboardImageWriterMock.Object,
                         trustedImageFileServiceMock.Object,
                         formatRegistryMock.Object,
                         uiThreadDispatcherMock.Object,
                         NullLogger<PicaViewerSession>.Instance))
        {
            await session.PrepareAsync(request, CancellationToken.None);

            PicaViewerRequest preparedRequest = session.Request
                ?? throw new InvalidOperationException("The Pica request was not prepared.");
            preparedRequest.Items.Should().ContainSingle();
            preparedRequest.Items[0].FileName.Should().Be("reference.png");
            preparedRequest.Actions.Should().ContainSingle(action =>
                action.Id == AtomicArtPicaActions.AttachId);
            materializedPath = preparedRequest.Items[0].FilePath;
            File.Exists(materializedPath).Should().BeTrue();
        }

        File.Exists(materializedPath).Should().BeFalse();
    }

    [Fact]
    public async Task DispatchCurrentImageAsync_WithAttachAction_ExecutesAtomicArtCommand()
    {
        const string modelId = "model";
        const string sourceImagePath = "images/source.png";
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            nameof(PicaViewerSessionTests),
            Guid.NewGuid().ToString("N"));
        string trustedImagePath = Path.Combine(directoryPath, "source.png");
        byte[] imageContent = [1, 2, 3, 4];
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(trustedImagePath, imageContent);
        Mock<IClipboardImageWriter> clipboardImageWriterMock = new();
        Mock<ITrustedImageFileService> trustedImageFileServiceMock = new();
        Mock<IGenerationImageFormatRegistry> formatRegistryMock = new();
        Mock<IGenerationImageFormat> formatMock = new();
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        trustedImageFileServiceMock
            .Setup(service => service.GetTrustedImagePath(sourceImagePath, modelId))
            .Returns(trustedImagePath);
        formatMock.SetupGet(format => format.ContentType).Returns(GenerationImageContentTypes.Png);
        formatRegistryMock
            .Setup(registry => registry.TryGetByFileName(
                "source.png",
                out It.Ref<IGenerationImageFormat?>.IsAny))
            .Returns((string? _, out IGenerationImageFormat? format) =>
            {
                format = formatMock.Object;
                return true;
            });
        uiThreadDispatcherMock
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        IReadOnlyList<AttachedImageDto>? attachedImages = null;
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(images =>
        {
            attachedImages = images;
            return Task.CompletedTask;
        });
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(
                new List<GalleryImageViewerItem>
                {
                    new(
                        ItemId,
                        new GalleryFileImageViewerSource(modelId, sourceImagePath, null))
                }),
            ItemId,
            attachCommand);

        try
        {
            await using PicaViewerSession session = new(
                clipboardImageWriterMock.Object,
                trustedImageFileServiceMock.Object,
                formatRegistryMock.Object,
                uiThreadDispatcherMock.Object,
                NullLogger<PicaViewerSession>.Instance);
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = session.Request
                ?? throw new InvalidOperationException("The Pica request was not prepared.");

            await session.DispatchCurrentImageAsync(
                AtomicArtPicaActions.Attach,
                preparedRequest.Items.Single(),
                CancellationToken.None);

            attachedImages.Should().ContainSingle();
            attachedImages?[0].FileName.Should().Be("source.png");
            attachedImages?[0].ContentType.Should().Be(GenerationImageContentTypes.Png);
            attachedImages?[0].Content.Should().Equal(imageContent);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }
}
