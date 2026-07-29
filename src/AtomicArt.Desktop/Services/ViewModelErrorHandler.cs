using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services;

public sealed class ViewModelErrorHandler : IViewModelErrorHandler
{
    private readonly ILogger<ViewModelErrorHandler> _logger;
    private readonly ILocalizationTextProvider _textProvider;

    public ViewModelErrorHandler(
        ILogger<ViewModelErrorHandler> logger,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(textProvider);

        _logger = logger;
        _textProvider = textProvider;
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

        return _textProvider.Get(GetUserMessageKey(exception));
    }

    public string GetUserMessageKey(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            DataRootMigrationCleanupException =>
                SettingsLocalizationKeys.DataRoot.CleanupFailed,
            DataRootMigrationException =>
                SettingsLocalizationKeys.DataRoot.MigrationFailed,
            FileRevealException => GalleryLocalizationKeys.Errors.FileRevealFailed,
            GenerationAttemptException or HttpRequestException =>
                GenerationFailureMessageResolver.GetLocalizationKey(exception),
            TaskCanceledException => GenerationUiLocalizationKeys.Errors.ApiUnavailable,
            ArgumentException => GenerationUiLocalizationKeys.Errors.Failed,
            InvalidOperationException => GenerationUiLocalizationKeys.Errors.Failed,
            _ => CommonLocalizationKeys.UnknownError
        };
    }
}
