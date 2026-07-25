using System.Runtime.Versioning;

using Avalonia.Controls;
using Avalonia.Platform;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Behaviors;

namespace AtomicArt.Desktop.Services.Windows;

internal sealed class WindowsVirtualFileDropAttachmentService
    : IVirtualFileDropAttachmentService,
      IDisposable
{
    private const string NativeWindowHandleDescriptor = "HWND";

    private readonly WindowsVirtualFileReader _virtualFileReader;
    private readonly VirtualFileDropInputSession _inputSession;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WindowsVirtualFileDropAttachmentService> _logger;

    private Window? _window;
    private WindowsDropTargetRegistration? _registration;

    public WindowsVirtualFileDropAttachmentService(
        WindowsVirtualFileReader virtualFileReader,
        VirtualFileDropInputSession inputSession,
        ILoggerFactory loggerFactory,
        ILogger<WindowsVirtualFileDropAttachmentService> logger)
    {
        ArgumentNullException.ThrowIfNull(virtualFileReader);
        ArgumentNullException.ThrowIfNull(inputSession);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _virtualFileReader = virtualFileReader;
        _inputSession = inputSession;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_window is not null)
        {
            throw new InvalidOperationException(
                "The virtual file drop service is already attached.");
        }

        _window = window;

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened += OnWindowOpened;
        window.Closed += OnWindowClosed;
    }

    public void Dispose()
    {
        Window? window = _window;
        _window = null;

        if (window is not null && OperatingSystem.IsWindows())
        {
            window.Opened -= OnWindowOpened;
            window.Closed -= OnWindowClosed;
        }

        DisposeRegistration(restoreInnerTarget: true);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (OperatingSystem.IsWindows() && _window is not null)
        {
            TryInstall(_window);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        DisposeRegistration(restoreInnerTarget: false);
    }

    [SupportedOSPlatform("windows")]
    private void TryInstall(Window window)
    {
        if (_registration is not null)
        {
            return;
        }

        IPlatformHandle? handle = window.TryGetPlatformHandle();

        if (handle is null
            || !string.Equals(
                handle.HandleDescriptor,
                NativeWindowHandleDescriptor,
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Virtual file drop support could not obtain the main window handle.");
            return;
        }

        try
        {
            _registration = WindowsDropTargetRegistration.TryCreate(
                handle.Handle,
                _virtualFileReader,
                _inputSession,
                () => ImageAttachmentBehavior.GetMaxInputBytes(window),
                _loggerFactory.CreateLogger<WindowsOleDropTargetProxy>(),
                _logger);

            if (_registration is not null)
            {
                _logger.LogInformation(
                    "Windows virtual file drop support was attached.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Windows virtual file drop support could not be attached. Error type {ErrorType}, HRESULT {HResult}.",
                ex.GetType().Name,
                ex.HResult);
        }
    }

    private void DisposeRegistration(bool restoreInnerTarget)
    {
        WindowsDropTargetRegistration? registration = Interlocked.Exchange(
            ref _registration,
            null);

        if (OperatingSystem.IsWindows())
        {
            registration?.Dispose(restoreInnerTarget);
        }
    }

}
