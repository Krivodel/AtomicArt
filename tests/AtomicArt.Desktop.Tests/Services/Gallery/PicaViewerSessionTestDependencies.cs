using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Pica.Viewer.Services;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal sealed class PicaViewerSessionTestDependencies
{
    internal Mock<IClipboardImageWriter> ClipboardImageWriter { get; } = new();
    internal Mock<ITrustedImageFileService> TrustedImageFileService { get; } = new();
    internal Mock<IGenerationImageFormatRegistry> FormatRegistry { get; } = new();
    internal Mock<IUiThreadDispatcher> UiThreadDispatcher { get; } = new();
    internal Mock<IWindowStateService> WindowStateService { get; } = new();
    internal Mock<IAnimatedGalleryOperations> GalleryOperations { get; } = new();
    internal Mock<IDlss5SourceOpener> Dlss5SourceOpener { get; } = new();
    internal Mock<IPanelAttachmentFilePathResolver> PanelAttachments { get; } = new();
    internal Mock<IGenerationReferenceStore> References { get; } = new();

    internal PicaViewerSession CreateSession(
        IGenerationImageFormatRegistry? formatRegistry = null)
    {
        return new PicaViewerSession(CreateDependencies(formatRegistry));
    }

    internal PicaViewerSessionDependencies CreateDependencies(
        IGenerationImageFormatRegistry? formatRegistry = null,
        IUiThreadDispatcher? uiThreadDispatcher = null,
        IPanelAttachmentFilePathResolver? panelAttachments = null,
        IGenerationReferenceStore? references = null)
    {
        return new PicaViewerSessionDependencies(
            ClipboardImageWriter.Object,
            TrustedImageFileService.Object,
            formatRegistry ?? FormatRegistry.Object,
            uiThreadDispatcher ?? UiThreadDispatcher.Object,
            WindowStateService.Object,
            GalleryOperations.Object,
            new AtomicArtPicaActions(TestLocalizationTextProvider.Default),
            Dlss5SourceOpener.Object,
            panelAttachments ?? PanelAttachments.Object,
            references ?? References.Object,
            NullLoggerFactory.Instance);
    }
}
