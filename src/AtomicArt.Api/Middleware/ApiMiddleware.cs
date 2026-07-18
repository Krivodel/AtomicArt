namespace AtomicArt.Api.Middleware;

public abstract class ApiMiddleware
{
    protected RequestDelegate Next { get; }
    protected ILogger Logger { get; }

    protected ApiMiddleware(RequestDelegate next, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        Next = next;
        Logger = logger;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return InvokeCoreAsync(context);
    }

    protected abstract Task InvokeCoreAsync(HttpContext context);
}
