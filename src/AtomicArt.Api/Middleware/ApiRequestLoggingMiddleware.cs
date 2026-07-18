using System.Diagnostics;

namespace AtomicArt.Api.Middleware;

public sealed class ApiRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiRequestLoggingMiddleware> _logger;

    public ApiRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<ApiRequestLoggingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "HTTP request {TraceIdentifier} with method {Method} was received.",
            context.TraceIdentifier,
            context.Request.Method);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                exception,
                "HTTP request {TraceIdentifier} with method {Method} was canceled by the client after {ElapsedMilliseconds} ms.",
                context.TraceIdentifier,
                context.Request.Method,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            _logger.LogInformation(
                "HTTP request {TraceIdentifier} with method {Method} completed with status code {StatusCode} in {ElapsedMilliseconds} ms.",
                context.TraceIdentifier,
                context.Request.Method,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
