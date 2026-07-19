namespace AtomicArt.Desktop.Services;

internal static class ViewModelOperationExecutor
{
    public static async Task ExecuteAsync(
        IViewModelErrorHandler errorHandler,
        Action<string?> setErrorMessage,
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(setErrorMessage);
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
        }
    }
}
