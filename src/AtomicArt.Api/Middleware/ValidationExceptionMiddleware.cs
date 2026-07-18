using Microsoft.AspNetCore.Mvc;

using FluentValidation;

using AtomicArt.Api.ErrorHandling;

namespace AtomicArt.Api.Middleware;

public sealed class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(
        RequestDelegate next,
        ILogger<ValidationExceptionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (ValidationException exception)
        {
            await HandleValidationExceptionAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleValidationExceptionAsync(
        HttpContext context,
        ValidationException exception)
    {
        ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(exception);

        _logger.LogWarning(
            exception,
            "HTTP request {TraceIdentifier} with method {Method} failed validation with {FailureCount} violations.",
            context.TraceIdentifier,
            context.Request.Method,
            exception.Errors.Count());

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response
            .WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: ProblemDetailsContentTypes.Json,
                cancellationToken: context.RequestAborted)
            .ConfigureAwait(false);
    }
}
