using Microsoft.Extensions.Logging;

using Pica.Viewer.Services;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class PicaViewerSessionDependencies
{
    public IClipboardImageWriter ClipboardImageWriter { get; }
    public ITrustedImageFileService TrustedImageFileService { get; }
    public IGenerationImageFormatRegistry FormatRegistry { get; }
    public IUiThreadDispatcher UiThreadDispatcher { get; }
    public IWindowStateService WindowStateService { get; }
    public IAnimatedGalleryOperations GalleryOperations { get; }
    public AtomicArtPicaActions Actions { get; }
    public IDlss5SourceOpener Dlss5SourceOpener { get; }

    internal ILogger<PicaViewerSession> Logger { get; }

    public PicaViewerSessionDependencies(
        IClipboardImageWriter clipboardImageWriter,
        ITrustedImageFileService trustedImageFileService,
        IGenerationImageFormatRegistry formatRegistry,
        IUiThreadDispatcher uiThreadDispatcher,
        IWindowStateService windowStateService,
        IAnimatedGalleryOperations galleryOperations,
        AtomicArtPicaActions actions,
        IDlss5SourceOpener dlss5SourceOpener,
        ILoggerFactory loggerFactory)
    {
        ClipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
        TrustedImageFileService = trustedImageFileService
            ?? throw new ArgumentNullException(nameof(trustedImageFileService));
        FormatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        UiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        WindowStateService = windowStateService
            ?? throw new ArgumentNullException(nameof(windowStateService));
        GalleryOperations = galleryOperations
            ?? throw new ArgumentNullException(nameof(galleryOperations));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Dlss5SourceOpener = dlss5SourceOpener ?? throw new ArgumentNullException(nameof(dlss5SourceOpener));
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Logger = loggerFactory.CreateLogger<PicaViewerSession>();
    }
}
