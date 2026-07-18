using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class ImageViewerServiceTests
{
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task OpenAsync_WithNoImages_DoesNotCreateWindow()
    {
        Mock<IImageViewerWindowFactory> windowFactoryMock = new();
        Mock<IClipboardImageWriter> clipboardImageWriterMock = new();
        Mock<ITrustedImageFileService> trustedImageFileServiceMock = new();
        Mock<IGenerationImageFormatRegistry> formatRegistryMock = new();
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        PicaViewerSessionDependencies sessionDependencies = new(
            clipboardImageWriterMock.Object,
            trustedImageFileServiceMock.Object,
            formatRegistryMock.Object,
            uiThreadDispatcherMock.Object,
            NullLoggerFactory.Instance);
        PicaViewerSessionFactory sessionFactory = new(sessionDependencies);
        ImageViewerService service = new(
            windowFactoryMock.Object,
            sessionFactory,
            NullLogger<ImageViewerService>.Instance);
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(
                new List<GalleryImageViewerItem>()),
            ItemId,
            null);

        await service.OpenAsync(request, CancellationToken.None);

        windowFactoryMock.Verify(
            factory => factory.CreateAsync(
                It.IsAny<PicaViewerRequest>(),
                It.IsAny<IViewerActionDispatcher>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
