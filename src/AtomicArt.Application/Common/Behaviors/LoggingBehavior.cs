using System.Diagnostics;

using Microsoft.Extensions.Logging;

using FluentValidation;
using MediatR;

using AtomicArt.Application.Common.Models;

namespace AtomicArt.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const string NullResultStatus = "Null";
    private const string SuccessfulResultStatus = "Success";

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        string requestName = typeof(TRequest).Name;
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Request {RequestName} processing started.", requestName);

        try
        {
            TResponse response = await next().ConfigureAwait(false);
            string responseStatus = GetResponseStatus(response);
            string? errorCode = GetErrorCode(response);

            if (errorCode is null)
            {
                _logger.LogInformation(
                    "Request {RequestName} completed with status {ResponseStatus} in {ElapsedMilliseconds} ms.",
                    requestName,
                    responseStatus,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Request {RequestName} completed with status {ResponseStatus} and error code {ErrorCode} in {ElapsedMilliseconds} ms.",
                    requestName,
                    responseStatus,
                    errorCode,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (ValidationException exception)
        {
            _logger.LogWarning(
                exception,
                "Request {RequestName} was rejected by validation after {ElapsedMilliseconds} ms.",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                exception,
                "Request {RequestName} was canceled after {ElapsedMilliseconds} ms.",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Request {RequestName} failed after {ElapsedMilliseconds} ms.",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private static string GetResponseStatus(TResponse response)
    {
        if (response is null)
        {
            return NullResultStatus;
        }

        return response is IResultMetadata result
            ? result.Status.ToString()
            : SuccessfulResultStatus;
    }

    private static string? GetErrorCode(TResponse response)
    {
        if (response is null)
        {
            return null;
        }

        return response is IResultMetadata result
            ? result.ErrorCode
            : null;
    }
}
