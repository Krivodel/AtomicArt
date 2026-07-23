using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IProviderGenerationStream : IAsyncDisposable
{
    string ContentType { get; }
    ProviderGenerationSummary? Summary { get; }

    Task CopyToAsync(
        Stream destination,
        long maximumBytes,
        CancellationToken ct);
}
