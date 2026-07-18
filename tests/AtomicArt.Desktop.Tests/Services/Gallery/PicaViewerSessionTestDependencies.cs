using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal sealed class PicaViewerSessionTestDependencies
{
    internal Mock<IClipboardImageWriter> ClipboardImageWriter { get; } = new();
    internal Mock<ITrustedImageFileService> TrustedImageFileService { get; } = new();
    internal Mock<IGenerationImageFormatRegistry> FormatRegistry { get; } = new();
    internal Mock<IUiThreadDispatcher> UiThreadDispatcher { get; } = new();

    internal PicaViewerSession CreateSession()
    {
        PicaViewerSessionDependencies dependencies = new(
            ClipboardImageWriter.Object,
            TrustedImageFileService.Object,
            FormatRegistry.Object,
            UiThreadDispatcher.Object,
            NullLoggerFactory.Instance);

        return new PicaViewerSession(dependencies);
    }
}
