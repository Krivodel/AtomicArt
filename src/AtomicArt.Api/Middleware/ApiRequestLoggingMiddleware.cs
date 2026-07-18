using System.Diagnostics;

namespace AtomicArt.Api.Middleware;

public sealed class ApiRequestLoggingMiddleware : ApiMiddleware
{
    public ApiRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<ApiRequestLoggingMiddleware> logger)
        : base(next, logger)
    {
    }

    protected override async Task InvokeCoreAsync(HttpContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Logger.LogInformation(
            "HTTP request {TraceIdentifier} with method {Method} was received.",
            context.TraceIdentifier,
            context.Request.Method);

        try
        {
            await Next(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (context.RequestAborted.IsCancellationRequested)
        {
            Logger.LogInformation(
                exception,
                "HTTP request {TraceIdentifier} with method {Method} was canceled by the client after {ElapsedMilliseconds} ms.",
                context.TraceIdentifier,
                context.Request.Method,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            Logger.LogInformation(
                "HTTP request {TraceIdentifier} with method {Method} completed with status code {StatusCode} in {ElapsedMilliseconds} ms.",
                context.TraceIdentifier,
                context.Request.Method,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
