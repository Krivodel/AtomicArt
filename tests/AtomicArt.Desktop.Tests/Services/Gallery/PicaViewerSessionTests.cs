using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;

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
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(imagePath, modelId))
            .Returns(trustedImagePath);
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePathOrDefault(thumbnailPath, modelId))
            .Returns(trustedThumbnailPath);
        GalleryImageViewerRequest request = CreateRequest(
            new GalleryFileImageViewerSource(modelId, imagePath, thumbnailPath),
            null);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest preparedRequest = GetPreparedRequest(session);
        preparedRequest.Items.Should().ContainSingle();
        preparedRequest.Items[0].FilePath.Should().Be(trustedImagePath);
        preparedRequest.Items[0].PreviewFilePath.Should().Be(trustedThumbnailPath);
    }

    [Fact]
    public async Task PrepareAsync_WithAttachedImage_MaterializesFileAndAttachAction()
    {
        PicaViewerSessionTestDependencies dependencies = new();
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(
            _ => Task.CompletedTask);
        AttachedImageDto image = new(
            "reference.png",
            GenerationImageContentTypes.Png,
            [1, 2, 3]);
        GalleryImageViewerRequest request = CreateRequest(
            new GalleryAttachedImageViewerSource(image),
            attachCommand);
        IGenerationImageFormatRegistry formatRegistry =
            GenerationImageFormatRegistryTestFactory.Create();
        string materializedPath;

        await using (PicaViewerSession session = dependencies.CreateSession(formatRegistry))
        {
            await session.PrepareAsync(request, CancellationToken.None);

            PicaViewerRequest preparedRequest = GetPreparedRequest(session);
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
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(sourceImagePath, modelId))
            .Returns(trustedImagePath);
        dependencies.UiThreadDispatcher
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
        GalleryImageViewerRequest request = CreateRequest(
            new GalleryFileImageViewerSource(modelId, sourceImagePath, null),
            attachCommand);
        IGenerationImageFormatRegistry formatRegistry =
            GenerationImageFormatRegistryTestFactory.Create();

        try
        {
            await using PicaViewerSession session = dependencies.CreateSession(formatRegistry);
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = GetPreparedRequest(session);

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

    private static GalleryImageViewerRequest CreateRequest(
        GalleryImageViewerSource source,
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? attachCommand)
    {
        List<GalleryImageViewerItem> items =
        [
            new GalleryImageViewerItem(ItemId, source)
        ];

        return new GalleryImageViewerRequest(
            new GalleryStaticImageViewerItemsSource(items),
            ItemId,
            attachCommand);
    }

    private static PicaViewerRequest GetPreparedRequest(PicaViewerSession session)
    {
        return session.Request
            ?? throw new InvalidOperationException("The Pica request was not prepared.");
    }
}
