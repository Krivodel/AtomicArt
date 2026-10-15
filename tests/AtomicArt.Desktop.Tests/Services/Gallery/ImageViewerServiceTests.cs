using Microsoft.Extensions.Logging.Abstractions;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Pica.Protocol;
using Pica.Viewer.Services;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class ImageViewerServiceTests
{
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task OpenAsync_WithNoImages_DoesNotCreateWindow()
    {
        Mock<IImageViewerWindowFactory> windowFactoryMock = new();
        using ImageViewerService service = ImageViewerServiceTests.CreateService(windowFactoryMock);
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(
                new List<GalleryImageViewerItem>()),
            ImageViewerServiceTests.ItemId,
            null);

        await service.OpenAsync(request, CancellationToken.None);

        windowFactoryMock.Verify(
            factory => factory.CreateAsync(
                It.IsAny<PicaViewerRequest>(),
                It.IsAny<IViewerActionDispatcher>(),
                It.IsAny<IReadOnlyDictionary<Guid, IPicaImageBitmapSource>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenAsync_WithConfiguredAttachmentsAndDlssBitmap_OffersDirectAttach()
    {
        Mock<IImageViewerWindowFactory> windowFactoryMock = new();
        PicaViewerRequest? preparedRequest = null;
        bool canDispatchBitmapWithoutEncoding = false;
        InvalidOperationException factoryFailure = new("Stop after preparing viewer actions.");
        windowFactoryMock
            .Setup(factory => factory.CreateAsync(
                It.IsAny<PicaViewerRequest>(),
                It.IsAny<IViewerActionDispatcher>(),
                It.IsAny<IReadOnlyDictionary<Guid, IPicaImageBitmapSource>?>(),
                It.IsAny<CancellationToken>()))
            .Callback((PicaViewerRequest request,
                IViewerActionDispatcher dispatcher,
                IReadOnlyDictionary<Guid, IPicaImageBitmapSource>? bitmapSources,
                CancellationToken ct) =>
            {
                preparedRequest = request;
                PicaActionDefinition attachAction = request.Actions.Single(action =>
                    string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));
                canDispatchBitmapWithoutEncoding = dispatcher.CanDispatchBitmapWithoutEncoding(
                    attachAction,
                    request.Items.Single());
            })
            .ThrowsAsync(factoryFailure);
        using ImageViewerService service = ImageViewerServiceTests.CreateService(windowFactoryMock);
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(
            _ => Task.CompletedTask);
        AsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?> inputCommand = new(
            _ => Task.CompletedTask);
        service.ConfigureAttachments(attachCommand, inputCommand);
        Mock<IPicaImageBitmapSource> bitmapSource = new();
        List<GalleryImageViewerItem> items =
        [
            new GalleryImageViewerItem(
                ImageViewerServiceTests.ItemId,
                new GalleryBitmapImageViewerSource(
                    "dlss5",
                    "dlss5-result.png",
                    bitmapSource.Object))
        ];
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(items),
            ImageViewerServiceTests.ItemId,
            null);

        Func<Task> act = () => service.OpenAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(exception => ReferenceEquals(exception, factoryFailure));
        PicaViewerRequest prepared = preparedRequest
            ?? throw new InvalidOperationException("Viewer actions were not prepared.");
        PicaActionDefinition attach = prepared.Actions.Single(action =>
            string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));
        attach.Targets.Should().Be(PicaActionTargets.CurrentImage | PicaActionTargets.Selection);
        canDispatchBitmapWithoutEncoding.Should().BeTrue();
    }

    private static ImageViewerService CreateService(
        Mock<IImageViewerWindowFactory> windowFactoryMock)
    {
        PicaViewerSessionTestDependencies dependencies = new();
        PicaViewerSessionFactory sessionFactory = new(dependencies.CreateDependencies());
        return new ImageViewerService(
            windowFactoryMock.Object,
            sessionFactory,
            new DataRootAccessCoordinator(),
            new ImageViewerWindowLimitService(new SettingsDefinitionCatalog(
                new ISettingsDefinition[] { new ImageViewerWindowLimitSettingDefinition() },
                Array.Empty<IUiScaleOptionDefinition>())),
            NullLogger<ImageViewerService>.Instance);
    }
}
