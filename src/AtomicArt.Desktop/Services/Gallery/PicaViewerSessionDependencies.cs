using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;

using Pica.Viewer.Services;

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

    internal ILogger<PicaViewerSession> Logger { get; }

    public PicaViewerSessionDependencies(
        IClipboardImageWriter clipboardImageWriter,
        ITrustedImageFileService trustedImageFileService,
        IGenerationImageFormatRegistry formatRegistry,
        IUiThreadDispatcher uiThreadDispatcher,
        IWindowStateService windowStateService,
        IAnimatedGalleryOperations galleryOperations,
        AtomicArtPicaActions actions,
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
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Logger = loggerFactory.CreateLogger<PicaViewerSession>();
    }
}
