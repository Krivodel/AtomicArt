using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels;

internal sealed class TestViewModelErrorHandler : IViewModelErrorHandler
{
    public int LogCallCount { get; private set; }

    private static readonly ViewModelErrorHandler MessageResolver =
        new(
            NullLogger<ViewModelErrorHandler>.Instance,
            TestLocalizationTextProvider.Default);

    public void Log(Exception exception, string operationName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        LogCallCount++;
    }

    public string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return MessageResolver.GetUserMessage(exception);
    }

    public string GetUserMessageKey(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return MessageResolver.GetUserMessageKey(exception);
    }
}
