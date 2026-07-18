namespace AtomicArt.Application.Common.Models;

public interface IResultMetadata
{
    ResultStatus Status { get; }
    string? ErrorCode { get; }
}
