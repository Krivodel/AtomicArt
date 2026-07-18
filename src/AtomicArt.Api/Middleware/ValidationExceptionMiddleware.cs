using Microsoft.AspNetCore.Mvc;

using FluentValidation;

using AtomicArt.Api.ErrorHandling;

namespace AtomicArt.Api.Middleware;

public sealed class ValidationExceptionMiddleware : ApiMiddleware
{
    public ValidationExceptionMiddleware(
        RequestDelegate next,
        ILogger<ValidationExceptionMiddleware> logger)
        : base(next, logger)
    {
    }

    protected override async Task InvokeCoreAsync(HttpContext context)
    {
        try
        {
            await Next(context).ConfigureAwait(false);
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

        Logger.LogWarning(
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
