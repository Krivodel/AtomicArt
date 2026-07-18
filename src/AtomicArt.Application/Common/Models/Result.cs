namespace AtomicArt.Application.Common.Models;

public sealed class Result<T> : IResultMetadata
{
    public T? Value { get; }
    public ResultStatus Status { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public bool IsSuccess => Status == ResultStatus.Success;
    public bool IsValidationError => Status == ResultStatus.ValidationError;
    public bool IsNotFound => Status == ResultStatus.NotFound;
    public bool IsUnavailable => Status == ResultStatus.Unavailable;

    private Result(T? value, ResultStatus status, string? errorCode, string? errorMessage)
    {
        Value = value;
        Status = status;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<T>(value, ResultStatus.Success, null, null);
    }

    public static Result<T> ValidationError(string errorCode, string errorMessage)
    {
        return CreateFailure(ResultStatus.ValidationError, errorCode, errorMessage);
    }

    public static Result<T> NotFound(string errorCode, string errorMessage)
    {
        return CreateFailure(ResultStatus.NotFound, errorCode, errorMessage);
    }

    public static Result<T> Unavailable(string errorCode, string errorMessage)
    {
        return CreateFailure(ResultStatus.Unavailable, errorCode, errorMessage);
    }

    private static Result<T> CreateFailure(ResultStatus status, string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new Result<T>(default, status, errorCode, errorMessage);
    }
}
