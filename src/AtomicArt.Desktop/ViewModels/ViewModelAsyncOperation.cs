using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels;

internal static class ViewModelAsyncOperation
{
    internal static async Task RunAsync(
        Func<Task> operation,
        CancellationToken ct,
        IViewModelErrorHandler errorHandler,
        string operationName,
        Action<bool> setIsLoading,
        Action<string?> setErrorMessage,
        Action<string?> setErrorLocalizationKey)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(setIsLoading);
        ArgumentNullException.ThrowIfNull(setErrorMessage);
        ArgumentNullException.ThrowIfNull(setErrorLocalizationKey);

        try
        {
            setIsLoading(true);
            setErrorMessage(null);
            setErrorLocalizationKey(null);
            await ExecuteAsync(
                errorHandler,
                setErrorMessage,
                setErrorLocalizationKey,
                _ => operation(),
                operationName,
                ct);
        }
        finally
        {
            setIsLoading(false);
        }
    }

    internal static async Task ExecuteAsync(
        IViewModelErrorHandler errorHandler,
        Action<string?> setErrorMessage,
        Action<string?> setErrorLocalizationKey,
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(setErrorMessage);
        ArgumentNullException.ThrowIfNull(setErrorLocalizationKey);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            await operation(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            errorHandler.Log(ex, operationName);
            setErrorMessage(errorHandler.GetUserMessage(ex));
            setErrorLocalizationKey(errorHandler.GetUserMessageKey(ex));
        }
    }
}
