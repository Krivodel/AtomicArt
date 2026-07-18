using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ViewModelErrorHandler : IViewModelErrorHandler
{
    private readonly ILogger<ViewModelErrorHandler> _logger;

    public ViewModelErrorHandler(ILogger<ViewModelErrorHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public void Log(Exception exception, string operationName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "ViewModel operation failed due to external or canceled operation: {OperationName}",
                operationName);

            return;
        }

        _logger.LogError(
            exception,
            "ViewModel operation failed unexpectedly: {OperationName}",
            operationName);
    }

    public string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            FileRevealException => UiStrings.FileRevealFailed,
            HttpRequestException => UiStrings.GenerationApiUnavailable,
            TaskCanceledException => UiStrings.GenerationApiUnavailable,
            ArgumentException => UiStrings.GenerationFailed,
            InvalidOperationException => UiStrings.GenerationFailed,
            _ => UiStrings.UnhandledExceptionMessage
        };
    }
}
