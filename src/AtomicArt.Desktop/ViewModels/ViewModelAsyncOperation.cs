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
        Action<string?> setErrorMessage)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(setIsLoading);
        ArgumentNullException.ThrowIfNull(setErrorMessage);

        try
        {
            setIsLoading(true);
            setErrorMessage(null);
            await operation();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            setErrorMessage(null);
        }
        catch (Exception ex)
        {
            errorHandler.Log(ex, operationName);
            setErrorMessage(errorHandler.GetUserMessage(ex));
        }
        finally
        {
            setIsLoading(false);
        }
    }
}
