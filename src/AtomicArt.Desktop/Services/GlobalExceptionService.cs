using Microsoft.Extensions.Logging;

using Avalonia.Threading;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GlobalExceptionService : IDisposable
{
    private readonly ILogger<GlobalExceptionService> _logger;
    private readonly IDialogService _dialogService;
    private bool _isInitialized;

    public GlobalExceptionService(
        ILogger<GlobalExceptionService> logger,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(dialogService);

        _logger = logger;
        _dialogService = dialogService;
    }

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        _isInitialized = true;
    }

    public void Dispose()
    {
        if (!_isInitialized)
        {
            return;
        }

        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        _isInitialized = false;
    }

    private bool TryShowSafeError()
    {
        try
        {
            return _dialogService.ShowError(UiStrings.UnhandledExceptionMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to show safe error dialog.");

            return false;
        }
    }

    private void OnDispatcherUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled Avalonia UI thread exception.");
        TryShowSafeError();
    }

    private void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled domain exception. IsTerminating: {IsTerminating}",
                e.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
