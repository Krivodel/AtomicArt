using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Input;
using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class ImageViewerService :
    IImageViewerService,
    IDataRootViewerPreparationService,
    IDisposable
{
    private readonly IImageViewerWindowFactory _windowFactory;
    private readonly PicaViewerSessionFactory _sessionFactory;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly ILogger<ImageViewerService> _logger;
    private readonly ImageViewerWindowLimitService _windowLimit;
    private readonly SemaphoreSlim _windowOperations = new(1, 1);
    private readonly object _syncRoot = new();
    private readonly List<PicaViewerSession> _sessions = [];
    private IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? _attachImagesCommand;
    private IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? _attachImageInputsCommand;

    public ImageViewerService(
        IImageViewerWindowFactory windowFactory,
        PicaViewerSessionFactory sessionFactory,
        IDataRootAccessCoordinator accessCoordinator,
        ImageViewerWindowLimitService windowLimit,
        ILogger<ImageViewerService> logger)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _windowLimit = windowLimit ?? throw new ArgumentNullException(nameof(windowLimit));
        _windowLimit.ValueChanged += OnWindowLimitChanged;
    }

    public void ConfigureAttachments(
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> command,
        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? inputCommand = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        _attachImagesCommand = command;
        _attachImageInputsCommand = inputCommand;
    }

    public async Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        using DataRootAccessLease accessLease =
            await _accessCoordinator.AcquireAccessAsync(ct);
        await _windowOperations.WaitAsync(ct);

        try
        {
            await OpenWindowAsync(request, ct);
            await EnforceWindowLimitCoreAsync(ct);
        }
        finally
        {
            _windowOperations.Release();
        }
    }

    public async Task CloseAllAsync(CancellationToken ct)
    {
        await _windowOperations.WaitAsync(ct);

        try
        {
            IReadOnlyList<PicaViewerSession> sessions;

            lock (_syncRoot)
            {
                sessions = _sessions.ToArray();
            }

            foreach (PicaViewerSession session in sessions)
            {
                await session.CloseAsync(ct);
            }
        }
        finally
        {
            _windowOperations.Release();
        }
    }

    public void Dispose()
    {
        _windowLimit.ValueChanged -= OnWindowLimitChanged;
        _windowOperations.Dispose();
    }

    internal async Task EnforceWindowLimitAsync(CancellationToken ct)
    {
        await _windowOperations.WaitAsync(ct);

        try
        {
            await EnforceWindowLimitCoreAsync(ct);
        }
        finally
        {
            _windowOperations.Release();
        }
    }

    private async Task OpenWindowAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Preparing an embedded Pica viewer session for selected item {ItemId}",
            request.SelectedItemId);

        PicaViewerSession session = _sessionFactory.Create();
        TrackSession(session);

        try
        {
            GalleryImageViewerRequest preparedRequest = request with
            {
                AttachImagesCommand = request.AttachImagesCommand ?? _attachImagesCommand,
                AttachImageInputsCommand = request.AttachImageInputsCommand ?? _attachImageInputsCommand
            };
            await session.PrepareAsync(preparedRequest, ct);
            PicaViewerRequest? viewerRequest = session.Request;

            if ((viewerRequest is null) || (viewerRequest.Items.Count == 0))
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
                session.BitmapSources,
                ct);
            session.AttachWindow(window);
            window.Show();
            _logger.LogInformation(
                "Embedded Pica viewer window opened for selected item {ItemId}",
                request.SelectedItemId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Embedded Pica viewer session failed during preparation or window creation");
            await session.DisposeAsync();
            throw;
        }
    }

    private async Task EnforceWindowLimitCoreAsync(CancellationToken ct)
    {
        while (_windowLimit.Limit != ImageViewerWindowLimitSettingDefinition.Unlimited)
        {
            PicaViewerSession oldest;

            lock (_syncRoot)
            {
                if (_sessions.Count <= _windowLimit.Limit)
                {
                    return;
                }

                oldest = _sessions[0];
            }

            await oldest.CloseAsync(ct);
        }
    }

    private void TrackSession(PicaViewerSession session)
    {
        lock (_syncRoot)
        {
            _sessions.Add(session);
            session.Disposed += OnSessionDisposed;
        }
    }

    private void OnSessionDisposed(object? sender, EventArgs eventArgs)
    {
        if (sender is not PicaViewerSession session)
        {
            return;
        }

        lock (_syncRoot)
        {
            session.Disposed -= OnSessionDisposed;
            _sessions.Remove(session);
        }
    }

    private async void OnWindowLimitChanged(object? sender, EventArgs eventArgs)
    {
        try
        {
            await EnforceWindowLimitAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to apply the image viewer window limit.");
        }
    }
}
