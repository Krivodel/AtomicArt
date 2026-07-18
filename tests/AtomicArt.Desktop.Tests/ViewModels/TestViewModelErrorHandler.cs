using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Tests.ViewModels;

internal sealed class TestViewModelErrorHandler : IViewModelErrorHandler
{
    public int LogCallCount { get; private set; }

    public void Log(Exception exception, string operationName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        LogCallCount++;
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
