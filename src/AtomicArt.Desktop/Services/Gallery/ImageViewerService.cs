using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class ImageViewerService : IImageViewerService
{
    private readonly IImageViewerWindowFactory _windowFactory;
    private readonly PicaViewerSessionFactory _sessionFactory;
    private readonly ILogger<ImageViewerService> _logger;

    public ImageViewerService(
        IImageViewerWindowFactory windowFactory,
        PicaViewerSessionFactory sessionFactory,
        ILogger<ImageViewerService> logger)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Preparing an embedded Pica viewer session for selected item {ItemId}",
            request.SelectedItemId);

        PicaViewerSession session = _sessionFactory.Create();

        try
        {
            await session.PrepareAsync(request, ct);
            PicaViewerRequest? viewerRequest = session.Request;

            if (viewerRequest is null || viewerRequest.Items.Count == 0)
            {
                _logger.LogWarning(
                    "Embedded Pica viewer request for selected item {ItemId} contained no usable images",
                    request.SelectedItemId);
                await session.DisposeAsync();
                return;
            }

            _logger.LogInformation(
                "Opening embedded Pica viewer with {ItemCount} images and {ActionCount} actions",
                viewerRequest.Items.Count,
                viewerRequest.Actions.Count);
            ImageViewerWindow window = await _windowFactory.CreateAsync(
                viewerRequest,
                session,
                ct);
            session.AttachWindow(window);
            window.Show();
            _logger.LogInformation(
                "Embedded Pica viewer window opened for selected item {ItemId}",
                request.SelectedItemId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Embedded Pica viewer session failed during preparation or window creation");
            await session.DisposeAsync();
            throw;
        }
    }
}
