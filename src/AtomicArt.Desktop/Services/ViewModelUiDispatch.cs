namespace AtomicArt.Desktop.Services;

internal static class ViewModelUiDispatch
{
    internal static Task RunAsync(
        IUiThreadDispatcher uiThreadDispatcher,
        Action action,
        CancellationToken ct,
        IViewModelErrorHandler errorHandler,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return RunCoreAsync(
            () => uiThreadDispatcher.InvokeAsync(action, ct),
            ct,
            errorHandler,
            operationName);
    }

    internal static Task RunAsync(
        IUiThreadDispatcher uiThreadDispatcher,
        Func<Task> action,
        CancellationToken ct,
        IViewModelErrorHandler errorHandler,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return RunCoreAsync(
            () => uiThreadDispatcher.InvokeAsync(action, ct),
            ct,
            errorHandler,
            operationName);
    }

    private static async Task RunCoreAsync(
        Func<Task> dispatch,
        CancellationToken ct,
        IViewModelErrorHandler errorHandler,
        string operationName)
    {
        try
        {
            await dispatch().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            errorHandler.Log(ex, operationName);
        }
    }
}
